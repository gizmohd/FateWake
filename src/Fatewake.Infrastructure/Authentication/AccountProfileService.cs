using Fatewake.Infrastructure.Persistence;
using Fatewake.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Authentication;

/// <summary>Saves a signed-in account's self-reported profile under its canonical ownership lock.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/AccountProfileService.md">Documentation</see>
public sealed class AccountProfileService(FatewakeDbContext db, ILogger<AccountProfileService> log)
{
    /// <summary>Serializes profile creation/updates and never changes identity links, credentials, or gameplay state.</summary>
    /// <returns>Null after a successful save; otherwise safe validation or account-availability feedback.</returns>
    public async Task<string?> CompleteAsync(Guid accountId, AccountProfileDetails details, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("auth.complete_profile", log);
        var normalized = details.Normalize();
        if (normalized is null) return AccountProfileDetails.ValidationError;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var account = await db.Accounts.FromSqlInterpolated(
                $"""SELECT * FROM account WHERE "Id" = {accountId} FOR UPDATE""").SingleOrDefaultAsync(ct);
            if (account?.Status != AccountStatus.Active) return "This account is unavailable.";
            var profile = await db.AccountProfiles.SingleOrDefaultAsync(row => row.AccountId == accountId, ct);
            if (profile is null)
            {
                profile = new() { AccountId = accountId };
                db.AccountProfiles.Add(profile);
            }
            profile.FirstName = normalized.FirstName;
            profile.LastName = normalized.LastName;
            profile.City = normalized.City;
            profile.State = normalized.State;
            profile.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            log.LogInformation("Registration profile saved for account {AccountId}", accountId);
            return null;
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            log.LogError("Account profile save failed with {ExceptionType}", ex.GetType().FullName);
            throw;
        }
    }
}
