using System.Security.Cryptography;
using System.Text;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Fatewake.Observability;

namespace Fatewake.Infrastructure.Authentication;

/// <summary>Coordinates mailbox proof, canonical email ownership, and safe external identity linking.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/VerifiedAccountService.md">Documentation</see>
public sealed class VerifiedAccountService(FatewakeDbContext db, VerificationEmailSender sender, ILogger<VerifiedAccountService> log,
    IPasswordHasher<LocalCredentialRecord> passwords)
{
    /// <summary>Persists pending local registration and sends mailbox proof without creating an authenticated account.</summary>
    public async Task<LocalAccountResult> RegisterAsync(string email, string passwordHash, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("auth.pending_registration", log);
        if (!EmailAddressNormalizer.TryNormalize(email, out var normalized))
            return new(null, null, "Enter a valid email address.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockEmailAsync(normalized, ct);
        if (await db.AccountEmails.AnyAsync(x => x.NormalizedEmail == normalized, ct))
            return new(null, null, "Unable to register this email address. Try signing in instead.");
        if (await RecentlySentAsync(normalized, null, null, ct)) return new(null, null, "A verification email was recently sent. Wait a minute before registering again.");
        var token = AddChallenge(email.Trim(), normalized, passwordHash, null);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await sender.SendAsync(email.Trim(), token, ct);
        return new(null, email.Trim(), null, true);
    }

    /// <summary>Resolves validated provider claims, requiring proven email before first canonical account linking.</summary>
    public async Task<LocalAccountResult> ExternalAsync(ExternalLogin login, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("auth.external", log);
        if (login.Provider is not ("Google" or "Microsoft") || string.IsNullOrWhiteSpace(login.Subject))
            return new(null, null, "Unsupported external identity.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockIdentityAsync(login.Provider, login.Subject, ct);
        var identity = await db.ExternalIdentities.SingleOrDefaultAsync(x => x.Provider == login.Provider && x.ProviderSubject == login.Subject, ct);
        if (identity is not null)
        {
            var existingEmail = await db.AccountEmails.SingleOrDefaultAsync(x => x.AccountId == identity.AccountId, ct);
            if (existingEmail?.Verified == true)
            {
                var account = await db.Accounts.SingleAsync(x => x.Id == identity.AccountId, ct);
                if (account.Status != AccountStatus.Active) return new(null, null, "Account is unavailable.");
                identity.LastLoginAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new(account.Id, account.PrimaryEmail, null);
            }
        }
        if (!EmailAddressNormalizer.TryNormalize(login.Email, out var normalized))
            return new(null, null, "The provider did not supply a usable email address.");
        await LockEmailAsync(normalized, ct);
        // Microsoft email/preferred_username is not evidence of mailbox ownership.
        if (login.Provider != "Google" || login.EmailVerified != true)
        {
            if (await RecentlySentAsync(normalized, login.Provider, login.Subject, ct)) return new(null, login.Email, null, true);
            var token = AddChallenge(login.Email!.Trim(), normalized, null, login);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            await sender.SendAsync(login.Email!.Trim(), token, ct);
            return new(null, login.Email.Trim(), null, true);
        }
        var result = await ResolveVerifiedExternalAsync(login, normalized, ct);
        if (result.AccountId is null) return result;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    /// <summary>Resends a pending challenge with a cooldown and generic account-existence behavior.</summary>
    public async Task ResendAsync(string? email, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("auth.resend_verification", log);
        if (!EmailAddressNormalizer.TryNormalize(email, out var normalized)) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockEmailAsync(normalized, ct);
        var now = DateTimeOffset.UtcNow;
        var previous = await db.EmailVerifications.Where(x => x.NormalizedEmail == normalized && x.ConsumedAt == null)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (previous is not null && previous.CreatedAt > now.AddMinutes(-1)) return;
        var owner = await db.AccountEmails.SingleOrDefaultAsync(x => x.NormalizedEmail == normalized, ct);
        if (owner?.Verified == true && previous?.Provider is null) return;
        string? hash = previous?.PasswordHash;
        ExternalLogin? external = previous?.Provider is {} provider && previous.Subject is {} subject
            ? new(provider, subject, previous.Email, false, previous.DisplayName, null) : null;
        var destination = previous?.Email;
        if (destination is null && owner is not null)
            destination = (await db.Accounts.SingleAsync(x => x.Id == owner.AccountId, ct)).PrimaryEmail;
        if (destination is null) return;
        var token = AddChallenge(destination, normalized, hash, external);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await sender.SendAsync(destination, token, ct);
    }

    /// <summary>Consumes mailbox proof transactionally; local registrations also require their chosen password.</summary>
    public async Task<LocalAccountResult> VerifyAsync(string? token, CancellationToken ct, string? password = null)
    {
        using var operation = OperationTelemetry.Start("auth.verify_email", log);
        const string invalid = "The verification link is invalid, expired, or already used.";
        if (token is null || token.Length != 64) return new(null, null, invalid);
        var hash = HashToken(token);
        var candidate = await db.EmailVerifications.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (candidate is null) return new(null, null, invalid);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (candidate.Provider is {} provider && candidate.Subject is {} subject)
            await LockIdentityAsync(provider, subject, ct);
        await LockEmailAsync(candidate.NormalizedEmail, ct);
        var proof = await db.EmailVerifications.FromSqlInterpolated(
            $"""SELECT * FROM email_verification WHERE "TokenHash" = {hash} FOR UPDATE""").SingleAsync(ct);
        await db.Entry(proof).ReloadAsync(ct);
        if (proof.ConsumedAt is not null || proof.ExpiresAt <= DateTimeOffset.UtcNow)
            return new(null, null, invalid);
        LocalAccountResult result;
        if (proof.Provider is {} externalProvider && proof.Subject is {} externalSubject)
            result = await ResolveVerifiedExternalAsync(
                new(externalProvider, externalSubject, proof.Email, true, proof.DisplayName, null), proof.NormalizedEmail, ct);
        else
        {
            var owner = await db.AccountEmails.SingleOrDefaultAsync(x => x.NormalizedEmail == proof.NormalizedEmail, ct);
            if (owner is null)
            {
                if (proof.PasswordHash is null) return new(null, null, invalid);
                var credential = new LocalCredentialRecord { NormalizedEmail = proof.NormalizedEmail, PasswordHash = proof.PasswordHash };
                if (password is null || password.Length is < 1 or > 128 ||
                    passwords.VerifyHashedPassword(credential, proof.PasswordHash, password) == PasswordVerificationResult.Failed)
                    return new(null, null, "Re-enter the password you chose when registering.");
                var account = NewAccount(proof.Email, null);
                owner = new() { AccountId = account.Id, NormalizedEmail = proof.NormalizedEmail, Verified = true };
                db.AccountEmails.Add(owner);
                db.LocalCredentials.Add(new() { AccountId = account.Id, NormalizedEmail = proof.NormalizedEmail, PasswordHash = proof.PasswordHash });
                result = new(account.Id, account.PrimaryEmail, null);
            }
            else
            {
                // A pending registration must never overwrite a subsequently created account.
                if (owner.Verified || proof.PasswordHash is not null) return new(null, null, invalid);
                owner.Verified = true;
                var account = await db.Accounts.SingleAsync(x => x.Id == owner.AccountId, ct);
                result = account.Status == AccountStatus.Active ? new(account.Id, account.PrimaryEmail, null) : new(null, null, "Account is unavailable.");
            }
        }
        if (result.AccountId is null) return result;
        proof.ConsumedAt = DateTimeOffset.UtcNow;
        proof.PasswordHash = null;
        await db.SaveChangesAsync(ct);
        await db.EmailVerifications.Where(x => x.NormalizedEmail == proof.NormalizedEmail && x.ConsumedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAt, DateTimeOffset.UtcNow).SetProperty(x => x.PasswordHash, (string?)null), ct);
        await tx.CommitAsync(ct);
        log.LogInformation("Email ownership verified for account {AccountId}", result.AccountId);
        return result;
    }

    private async Task<LocalAccountResult> ResolveVerifiedExternalAsync(ExternalLogin login, string normalized, CancellationToken ct)
    {
        var identity = await db.ExternalIdentities.SingleOrDefaultAsync(x => x.Provider == login.Provider && x.ProviderSubject == login.Subject, ct);
        var owner = await db.AccountEmails.SingleOrDefaultAsync(x => x.NormalizedEmail == normalized, ct);
        if (identity is not null && owner is not null && identity.AccountId != owner.AccountId)
            return new(null, null, "This provider identity is already linked to another account.");
        AccountRecord account;
        if (owner is not null)
        {
            account = await db.Accounts.SingleAsync(x => x.Id == owner.AccountId, ct);
            if (account.Status != AccountStatus.Active) return new(null, null, "Account is unavailable.");
            if (!owner.Verified)
            {
                // Never retain an unverified pre-registration password when the real mailbox owner arrives.
                var unverifiedPassword = await db.LocalCredentials.SingleOrDefaultAsync(x => x.AccountId == owner.AccountId, ct);
                if (unverifiedPassword is not null) db.LocalCredentials.Remove(unverifiedPassword);
                owner.Verified = true;
            }
        }
        else
        {
            account = identity is null ? NewAccount(login.Email!.Trim(), login.DisplayName)
                : await db.Accounts.SingleAsync(x => x.Id == identity.AccountId, ct);
            if (await db.AccountEmails.AnyAsync(x => x.AccountId == account.Id, ct))
                return new(null, null, "This provider identity has a different account email.");
            db.AccountEmails.Add(new() { AccountId = account.Id, NormalizedEmail = normalized, Verified = true });
            account.PrimaryEmail = login.Email!.Trim();
        }
        if (account.Status != AccountStatus.Active) return new(null, null, "Account is unavailable.");
        var now = DateTimeOffset.UtcNow;
        if (identity is null)
            db.ExternalIdentities.Add(new() { Id = Guid.NewGuid(), AccountId = account.Id, Provider = login.Provider,
                ProviderSubject = login.Subject, Email = login.Email, EmailVerified = true, DisplayName = login.DisplayName,
                LinkedAt = now, LastLoginAt = now });
        else { identity.EmailVerified = true; identity.LastLoginAt = now; }
        log.LogInformation("Verified external identity linked to account {AccountId}", account.Id);
        return new(account.Id, account.PrimaryEmail, null);
    }

    private AccountRecord NewAccount(string email, string? displayName)
    {
        var now = DateTimeOffset.UtcNow;
        var account = new AccountRecord { Id = Guid.NewGuid(), PrimaryEmail = email, DisplayName = displayName,
            Status = AccountStatus.Active, CreatedAt = now, UpdatedAt = now };
        db.Accounts.Add(account);
        return account;
    }

    private string AddChallenge(string email, string normalized, string? passwordHash, ExternalLogin? external)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;
        db.EmailVerifications.Add(new() { Id = Guid.NewGuid(), Email = email, NormalizedEmail = normalized,
            TokenHash = HashToken(token), PasswordHash = passwordHash, Provider = external?.Provider,
            Subject = external?.Subject, DisplayName = external?.DisplayName, CreatedAt = now, ExpiresAt = now.AddHours(24) });
        return token;
    }

    private Task LockEmailAsync(string normalized, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({normalized}, 0))", ct);
    private Task LockIdentityAsync(string provider, string subject, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({provider + ":" + subject}, 1))", ct);
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private Task<bool> RecentlySentAsync(string normalized, string? provider, string? subject, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-1);
        return db.EmailVerifications.AnyAsync(x => x.NormalizedEmail == normalized && x.Provider == provider && x.Subject == subject &&
            x.CreatedAt > cutoff && x.ConsumedAt == null, ct);
    }
}
