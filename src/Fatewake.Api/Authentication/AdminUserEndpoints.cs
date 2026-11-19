using System.Security.Claims;
using System.Security.Cryptography;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Observability;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace Fatewake.Api.Authentication;

/// <summary>Manages canonical users and verifies administrator-requested mailbox challenges.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/Authentication/AdminUserEndpoints.md">Documentation</see>
public sealed class AdminUserEndpoints(FatewakeDbContext db, VerificationEmailSender mail,
    IPasswordHasher<LocalCredentialRecord> passwords, IOpenIddictAuthorizationManager authorizations,
    IOpenIddictTokenManager tokens, ILogger<AdminUserEndpoints> log)
{
    /// <summary>Maps administrative operations and the anonymous single-use proof endpoint.</summary>
    public static void Map(WebApplication app)
    {
        var routes = app.MapGroup("/api/admin/accounts").RequireAuthorization("IdentityAdministrator");
        routes.MapGet("/", (AdminUserEndpoints service, string? search, int? page, CancellationToken ct) =>
            service.ReadAsync(null, search, page ?? 1, ct));
        routes.MapGet("/{id:guid}", (AdminUserEndpoints service, Guid id, CancellationToken ct) =>
            service.ReadAsync(id, null, 1, ct));
        routes.MapPost("/", (AdminUserEndpoints service, HttpContext context, AdminUserRequest request, CancellationToken ct) =>
            service.InviteAsync(context, request, ct));
        routes.MapPut("/{id:guid}", (AdminUserEndpoints service, HttpContext context, Guid id, AdminUserRequest request, CancellationToken ct) =>
            service.UpdateAsync(context, id, request, null, ct));
        routes.MapDelete("/{id:guid}", (AdminUserEndpoints service, HttpContext context, Guid id, DateTimeOffset version, Guid confirm, CancellationToken ct) =>
            service.UpdateAsync(context, id, new(null, null, null, "Disabled", version), confirm, ct));
        routes.MapPost("/{id:guid}/password-reset", (AdminUserEndpoints service, HttpContext context, Guid id, CancellationToken ct) =>
            service.ResetAsync(context, id, ct));
        routes.MapPost("/{id:guid}/resend-invitation", (AdminUserEndpoints service, HttpContext context, Guid id, CancellationToken ct) =>
            service.ResetAsync(context, id, ct, invitation: true));
        app.MapPost("/api/auth/account-access", (AdminUserEndpoints service, VerificationRequest request, CancellationToken ct) =>
            service.CompleteAsync(request.Token, request.Password, ct)).RequireRateLimiting("local-auth");
        app.MapPost("/api/auth/forgot-password", (AdminUserEndpoints service, EmailRequest request, CancellationToken ct) =>
            service.RequestPasswordResetAsync(request.Email, ct)).RequireRateLimiting("local-auth");
    }

    private async Task<IResult> ReadAsync(Guid? id, string? search, int page, CancellationToken ct)
    {
        if (page is < 1 or > 10000 || search?.Length > 100)
            return Results.BadRequest();
        using var operation = OperationTelemetry.Start("admin.user_read", log);
        var query = db.Accounts.AsNoTracking();
        if (id is not null)
            query = query.Where(row => row.Id == id);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(row => row.PrimaryEmail!.ToLower().Contains(term) || row.DisplayName!.ToLower().Contains(term));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(row => row.PrimaryEmail).ThenBy(row => row.Id).Skip((page - 1) * 25).Take(25)
            .Select(row => new
            {
                row.Id,
                email = row.PrimaryEmail,
                row.DisplayName,
                status = row.Status.ToString(),
                version = row.UpdatedAt,
                role = db.Set<AccountRoleRecord>().Where(r => r.AccountId == row.Id).Select(r => r.Role).FirstOrDefault() ?? "User",
                verified = db.AccountEmails.Any(e => e.AccountId == row.Id && e.Verified),
                hasPassword = db.LocalCredentials.Any(c => c.AccountId == row.Id)
            }).ToListAsync(ct);
        return id is null ? Results.Ok(new
        {
            total,
            rows
        }) : rows.Count == 0 ? Results.NotFound() : Results.Ok(rows[0]);
    }

    private async Task<IResult> InviteAsync(HttpContext context, AdminUserRequest request, CancellationToken ct)
    {
        if (!EmailAddressNormalizer.TryNormalize(request.Email, out var normalized) || request.DisplayName?.Length > 100 ||
            request.Role is not ("Admin" or "User"))
            return Results.BadRequest(new
            {
                error = "Provide a valid email address, name and role."
            });
        using var operation = OperationTelemetry.Start("admin.user_invite", log);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockEmailAsync(normalized, ct);
        if (await db.AccountEmails.AnyAsync(x => x.NormalizedEmail == normalized, ct))
            return Results.Conflict(new
            {
                error = "That email already belongs to an account."
            });
        var now = DateTimeOffset.UtcNow;
        var account = new AccountRecord
        {
            Id = Guid.NewGuid(),
            PrimaryEmail = request.Email!.Trim(),
            DisplayName = request.DisplayName?.Trim(),
            Status = AccountStatus.Invited,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Accounts.Add(account);
        db.AccountEmails.Add(new()
        {
            AccountId = account.Id,
            NormalizedEmail = normalized
        });
        db.Set<AccountRoleRecord>().Add(new()
        {
            AccountId = account.Id,
            Role = request.Role,
            AssignedAt = now
        });
        await db.SaveChangesAsync(ct);
        var token = await AddProofAsync(account.Id, "invite", account.PrimaryEmail, normalized, ct);
        await AuditAsync(context, account.Id, "user.invite", ct);
        await tx.CommitAsync(ct);
        return await DeliverAsync(account.PrimaryEmail, token, true, operation, ct);
    }

    private async Task<IResult> UpdateAsync(HttpContext context, Guid id, AdminUserRequest request, Guid? confirmed, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("admin.user_update", log);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(861017234)", ct);
        if (request.Email is not null)
        {
            if (!EmailAddressNormalizer.TryNormalize(request.Email, out var emailKey))
                return Results.BadRequest(new
                {
                    error = "Invalid email address."
                });
            await LockEmailAsync(emailKey, ct);
        }
        var account = await LockAccountAsync(id, ct);
        if (account is null)
            return Results.NotFound();
        if (account.UpdatedAt != request.Version)
            return Results.Conflict(new
            {
                error = "The account was updated. Reload the details."
            });
        var deletion = confirmed is not null;
        if (deletion && confirmed != id)
            return Results.BadRequest(new
            {
                error = "Confirm the exact account ID."
            });
        var assignedRole = await db.Set<AccountRoleRecord>().SingleOrDefaultAsync(x => x.AccountId == id, ct);
        var role = deletion ? assignedRole?.Role ?? "User" : request.Role;
        var status = request.Status switch
        {
            "Active" => AccountStatus.Active,
            "Disabled" => AccountStatus.Disabled,
            "Invited" => AccountStatus.Invited,
            _ => (AccountStatus?)null
        };
        var verified = await db.AccountEmails.AnyAsync(x => x.AccountId == id && x.Verified, ct);
        if (role is not ("Admin" or "User") || status is null || request.DisplayName?.Length > 100 ||
            status != account.Status && (status switch
            {
                AccountStatus.Disabled => !deletion,
                AccountStatus.Active => !verified,
                AccountStatus.Invited => account.Status != AccountStatus.Disabled || verified,
                _ => true
            }))
            return Results.BadRequest(new
            {
                error = "Invalid role, name or account status change."
            });
        if (assignedRole?.Role == "Admin" && (role != "Admin" || status != AccountStatus.Active) && account.Status == AccountStatus.Active)
        {
            var otherAdmin = await db.Accounts.AnyAsync(x => x.Id != id && x.Status == AccountStatus.Active &&
                db.AccountEmails.Any(e => e.AccountId == x.Id && e.Verified) &&
                db.Set<AccountRoleRecord>().Any(r => r.AccountId == x.Id && r.Role == "Admin"), ct);
            if (Actor(context) == id || !otherAdmin)
                return Results.Conflict(new
                {
                    error = "You cannot remove your own access or the last active administrator."
                });
        }

        string? token = null;
        string? destination = null;
        if (!deletion)
        {
            account.DisplayName = request.DisplayName?.Trim();
            if (assignedRole is null)
                db.Set<AccountRoleRecord>().Add(new()
                {
                    AccountId = id,
                    Role = role,
                    AssignedAt = DateTimeOffset.UtcNow
                });
            else
                assignedRole.Role = role;
            if (!string.Equals(account.PrimaryEmail, request.Email?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                if (status != AccountStatus.Active || account.Status != AccountStatus.Active ||
                    !EmailAddressNormalizer.TryNormalize(request.Email, out var normalized))
                    return Results.BadRequest(new
                    {
                        error = "Only active accounts can change their email address."
                    });
                if (await db.AccountEmails.AnyAsync(x => x.NormalizedEmail == normalized, ct))
                    return Results.Conflict(new
                    {
                        error = "That email already belongs to an account."
                    });
                destination = request.Email!.Trim();
                token = await AddProofAsync(id, "email", destination, normalized, ct);
            }
        }
        if (status == AccountStatus.Disabled && account.Status != AccountStatus.Disabled)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM identity_admin_proof WHERE account_id = {id}", ct);
            await RevokeAsync(id, ct);
        }
        account.Status = status.Value;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await AuditAsync(context, id, deletion ? "user.deactivate" : "user.update", ct);
        await tx.CommitAsync(ct);
        return token is null ? Results.NoContent() : await DeliverAsync(destination!, token, false, operation, ct);
    }

    private async Task<IResult> ResetAsync(HttpContext context, Guid id, CancellationToken ct, bool invitation = false)
    {
        using var operation = OperationTelemetry.Start("admin.user_send_proof", log);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var account = await LockAccountAsync(id, ct);
        if (account is null)
            return Results.NotFound();
        var email = await db.AccountEmails.SingleOrDefaultAsync(x => x.AccountId == id, ct);
        if (account.Status != (invitation ? AccountStatus.Invited : AccountStatus.Active) || email is null ||
            email.Verified == invitation || account.PrimaryEmail is null)
            return Results.Conflict(new
            {
                error = "The account is not eligible for this email."
            });
        var token = await AddProofAsync(id, invitation ? "invite" : "reset", account.PrimaryEmail, email.NormalizedEmail, ct);
        await AuditAsync(context, id, invitation ? "user.resend_invitation" : "user.reset_requested", ct);
        await tx.CommitAsync(ct);
        return await DeliverAsync(account.PrimaryEmail, token, true, operation, ct);
    }

    private async Task<IResult> CompleteAsync(string? token, string? password, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("admin.user_complete_proof", log);
        var invalid = Results.BadRequest(new
        {
            error = "The link is invalid, expired or already used, or the password does not meet requirements."
        });
        if (token is not { Length: 64 } || !token.All(char.IsAsciiHexDigit))
            return invalid;
        var hash = Hash(token);
        var proof = await FindProofAsync(hash, ct);
        if (proof is null || (proof.Purpose is "invite" or "reset" && password is not { Length: >= 12 and <= 128 }))
            return invalid;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (proof.Purpose is "invite" or "email")
            await LockEmailAsync(proof.NormalizedEmail, ct);
        var account = await LockAccountAsync(proof.AccountId, ct);
        if (account is null || account.Status != (proof.Purpose == "invite" ? AccountStatus.Invited : AccountStatus.Active) ||
            await FindProofAsync(hash, ct) is null)
            return invalid;
        var email = await db.AccountEmails.SingleOrDefaultAsync(x => x.AccountId == account.Id, ct);
        if (email is null || email.Verified == (proof.Purpose == "invite"))
            return invalid;
        var credential = await db.LocalCredentials.FromSqlInterpolated(
            $"""SELECT * FROM local_credential WHERE "AccountId" = {account.Id} FOR UPDATE""").SingleOrDefaultAsync(ct);
        if (proof.Purpose == "email")
        {
            if (await db.AccountEmails.AnyAsync(x => x.NormalizedEmail == proof.NormalizedEmail, ct))
                return Results.Conflict(new
                {
                    error = "That email already belongs to an account."
                });
            email.NormalizedEmail = proof.NormalizedEmail;
            account.PrimaryEmail = proof.Email;
            if (credential is not null)
                credential.NormalizedEmail = proof.NormalizedEmail;
        }
        else
        {
            if (proof.NormalizedEmail != email.NormalizedEmail)
                return invalid;
            if (credential is null)
            {
                credential = new LocalCredentialRecord { AccountId = account.Id, NormalizedEmail = email.NormalizedEmail, PasswordHash = "" };
                db.LocalCredentials.Add(credential);
            }
            credential.PasswordHash = passwords.HashPassword(credential, password!);
            credential.FailedAttempts = 0;
            credential.LockoutEnd = null;
            email.Verified = true;
            account.Status = AccountStatus.Active;
        }
        account.UpdatedAt = DateTimeOffset.UtcNow;
        await RevokeAsync(account.Id, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM identity_admin_proof WHERE account_id = {account.Id}", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        log.LogInformation("Account proof {Purpose} completed for account {AccountId}", proof.Purpose, account.Id);
        return Results.NoContent();
    }

    private async Task<IResult> RequestPasswordResetAsync(string? email, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("auth.request_password_reset", log);
        if (!EmailAddressNormalizer.TryNormalize(email, out var normalized))
            return Results.Accepted();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var id = await db.AccountEmails.Where(x => x.NormalizedEmail == normalized && x.Verified)
            .Select(x => (Guid?)x.AccountId).SingleOrDefaultAsync(ct);
        if (id is null)
            return Results.Accepted();
        var account = await LockAccountAsync(id.Value, ct);
        if (account?.Status != AccountStatus.Active || account.PrimaryEmail is null ||
            !await db.AccountEmails.AnyAsync(x => x.AccountId == account.Id && x.NormalizedEmail == normalized && x.Verified, ct) ||
            !await db.LocalCredentials.AnyAsync(x => x.AccountId == account.Id, ct))
            return Results.Accepted();
        var threshold = DateTimeOffset.UtcNow.AddMinutes(55);
        var recentlyRequested = await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM identity_admin_proof
                WHERE account_id = {account.Id} AND purpose = 'reset' AND expires_at > {threshold}) AS "Value"
            """).SingleAsync(ct);
        if (recentlyRequested)
            return Results.Accepted();
        var reset = await AddProofAsync(account.Id, "reset", account.PrimaryEmail, normalized, ct);
        await tx.CommitAsync(ct);
        try
        {
            await mail.SendAccountActionAsync(account.PrimaryEmail, reset, true, ct);
        }
        catch (Exception ex) when (ex is IOException or System.Net.Mail.SmtpException)
        {
            operation.Fail(ex);
            log.LogError("Self-service password reset email delivery failed with {ErrorType}", ex.GetType().Name);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM identity_admin_proof WHERE token_hash = {Hash(reset)}", ct);
        }
        return Results.Accepted();
    }

    private Task<AccountRecord?> LockAccountAsync(Guid id, CancellationToken ct) =>
        db.Accounts.FromSqlInterpolated($"""SELECT * FROM account WHERE "Id" = {id} FOR UPDATE""").SingleOrDefaultAsync(ct);

    private Task LockEmailAsync(string normalized, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({normalized}, 0))", ct);

    private async Task<string> AddProofAsync(Guid id, string purpose, string email, string normalized, CancellationToken ct)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO identity_admin_proof (token_hash, account_id, purpose, email, normalized_email, expires_at)
            VALUES ({Hash(token)}, {id}, {purpose}, {email}, {normalized}, {DateTimeOffset.UtcNow.AddHours(1)})
            ON CONFLICT (account_id, purpose) DO UPDATE SET token_hash = EXCLUDED.token_hash,
                email = EXCLUDED.email, normalized_email = EXCLUDED.normalized_email, expires_at = EXCLUDED.expires_at
            """, ct);
        return token;
    }

    private Task<AdminAccountProof?> FindProofAsync(string hash, CancellationToken ct) =>
        db.Database.SqlQuery<AdminAccountProof>($"""
            SELECT account_id AS "AccountId", purpose AS "Purpose", email AS "Email", normalized_email AS "NormalizedEmail"
            FROM identity_admin_proof WHERE token_hash = {hash} AND expires_at > CURRENT_TIMESTAMP
            """).SingleOrDefaultAsync(ct);

    private async Task RevokeAsync(Guid id, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE account SET "AccessRevokedAt" = {DateTimeOffset.UtcNow} WHERE "Id" = {id}""", ct);
        await tokens.RevokeBySubjectAsync(id.ToString(), ct);
        await authorizations.RevokeBySubjectAsync(id.ToString(), ct);
    }

    private async Task<IResult> DeliverAsync(string email, string token, bool password, OperationTelemetry operation, CancellationToken ct)
    {
        try
        {
            await mail.SendAccountActionAsync(email, token, password, ct);
            return Results.Accepted();
        }
        catch (Exception ex) when (ex is IOException or System.Net.Mail.SmtpException)
        {
            operation.Fail(ex);
            log.LogError("Account action email delivery failed with {ErrorType}", ex.GetType().Name);
            return Results.Json(new
            {
                error = "The change was saved, but email delivery failed. Retry the email action."
            }, statusCode: 503);
        }
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Convert.FromHexString(token)));

    private static Guid Actor(HttpContext context) => Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task AuditAsync(HttpContext context, Guid target, string action, CancellationToken ct)
    {
        var actor = Actor(context);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO identity_admin_audit (actor, target, action, result, occurred_at, correlation)
            VALUES ({actor}, {target.ToString()}, {action}, 'succeeded', {DateTimeOffset.UtcNow}, {context.TraceIdentifier})
            """, ct);
        log.LogInformation("Admin mutation {Action} targeted account {AccountId} by {AdminAccountId}", action, target, actor);
    }
}
