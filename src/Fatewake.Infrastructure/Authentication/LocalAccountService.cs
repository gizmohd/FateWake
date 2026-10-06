using Fatewake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Fatewake.Observability;

namespace Fatewake.Infrastructure.Authentication;

/// <summary>Registers and authenticates local accounts using salted password hashes and serialized lockout updates.</summary>
/// <param name="db">Scoped canonical PostgreSQL persistence context.</param>
/// <param name="passwords">Versioned password hashing and verification.</param>
/// <param name="log">Structured account-operation logging without credentials.</param>
/// <param name="verification">Verified email ownership and pending registration workflow.</param>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/LocalAccountService.md">LocalAccountService documentation</see>
public sealed class LocalAccountService(
    FatewakeDbContext db,
    IPasswordHasher<LocalCredentialRecord> passwords,
    ILogger<LocalAccountService> log,
    VerifiedAccountService verification)
{
    private const string InvalidLogin = "Invalid email or password.";

    /// <summary>Creates an account and unique local credential in one transaction without linking external identities by email.</summary>
    /// <param name="email">Email supplied for registration.</param>
    /// <param name="password">Password to hash; never persisted or logged.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created account or a safe registration error.</returns>
    public async Task<LocalAccountResult> RegisterAsync(string? email, string? password, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("auth.register", log);
        if (!EmailAddressNormalizer.TryNormalize(email, out var normalized))
            return new(null, null, "Enter a valid email address.");
        if (password is null || password.Length is < 12 or > 128)
            return new(null, null, "Use a password between 12 and 128 characters.");

        var id = Guid.NewGuid();
        var credential = new LocalCredentialRecord { AccountId = id, NormalizedEmail = normalized, PasswordHash = "" };
        credential.PasswordHash = passwords.HashPassword(credential, password);
        return await verification.RegisterAsync(email!.Trim(), credential.PasswordHash, ct);
    }

    /// <summary>Verifies a password and atomically updates failure counts, lockout, and hash upgrades.</summary>
    /// <param name="email">Email supplied for login.</param>
    /// <param name="password">Password to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The account on success or a generic invalid-credentials result.</returns>
    public async Task<LocalAccountResult> LoginAsync(string? email, string? password, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("auth.login", log);
        if (!EmailAddressNormalizer.TryNormalize(email, out var normalized) || password is null || password.Length is < 1 or > 128)
            return new(null, null, InvalidLogin);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var credential = await db.LocalCredentials.FromSqlInterpolated(
            $"""SELECT * FROM local_credential WHERE "NormalizedEmail" = {normalized} FOR UPDATE""").SingleOrDefaultAsync(ct);
        if (credential is null)
        {
            var dummy = new LocalCredentialRecord { NormalizedEmail = normalized, PasswordHash = "" };
            passwords.HashPassword(dummy, password);
            log.LogInformation("Local login rejected");
            return new(null, null, InvalidLogin);
        }
        var now = DateTimeOffset.UtcNow;
        if (credential.LockoutEnd > now)
        {
            log.LogInformation("Local login blocked by account lockout");
            return new(null, null, InvalidLogin);
        }
        if (credential.LockoutEnd is not null) { credential.FailedAttempts = 0; credential.LockoutEnd = null; }
        var verified = passwords.VerifyHashedPassword(credential, credential.PasswordHash, password);
        var account = await db.Accounts.SingleAsync(x => x.Id == credential.AccountId, ct);
        if (verified == PasswordVerificationResult.Failed || account.Status != AccountStatus.Active)
        {
            credential.FailedAttempts++;
            if (credential.FailedAttempts >= 5) credential.LockoutEnd = now.AddMinutes(15);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            log.LogInformation("Local password verification rejected for account {AccountId}", credential.AccountId);
            return new(null, null, InvalidLogin);
        }
        if (!await db.AccountEmails.AnyAsync(x => x.AccountId == account.Id && x.Verified, ct))
            return new(null, null, "Verify your email before signing in.", true);
        credential.FailedAttempts = 0;
        credential.LockoutEnd = null;
        if (verified == PasswordVerificationResult.SuccessRehashNeeded)
            credential.PasswordHash = passwords.HashPassword(credential, password);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(account.Id, account.PrimaryEmail, null);
    }

    /// <summary>Sets the initial local password for a verified external account; existing passwords are never overwritten.</summary>
    public async Task<LocalAccountResult> SetPasswordAsync(Guid accountId, string? password, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("auth.set_password", log);
        if (password is null || password.Length is < 12 or > 128)
            return new(null, null, "Use a password between 12 and 128 characters.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var account = await db.Accounts.FromSqlInterpolated($"""SELECT * FROM account WHERE "Id" = {accountId} FOR UPDATE""").SingleAsync(ct);
        var email = await db.AccountEmails.SingleOrDefaultAsync(x => x.AccountId == accountId && x.Verified, ct);
        if (email is null || account.Status != AccountStatus.Active) return new(null, null, "Verify your email first.");
        if (await db.LocalCredentials.AnyAsync(x => x.AccountId == accountId, ct))
            return new(null, null, "A local password is already configured.");
        var credential = new LocalCredentialRecord { AccountId = accountId, NormalizedEmail = email.NormalizedEmail, PasswordHash = "" };
        credential.PasswordHash = passwords.HashPassword(credential, password);
        db.LocalCredentials.Add(credential);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        log.LogInformation("Local password configured for account {AccountId}", accountId);
        return new(accountId, account.PrimaryEmail, null);
    }
}
