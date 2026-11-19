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
        if (normalized is null)
            return AccountProfileDetails.ValidationError;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (!await LockActiveAccountAsync(accountId, ct))
                return "This account is unavailable.";
            var profile = await db.AccountProfiles.SingleOrDefaultAsync(row => row.AccountId == accountId, ct);
            if (profile is null)
            {
                profile = new()
                {
                    AccountId = accountId
                };
                db.AccountProfiles.Add(profile);
            }
            var birthDate = profile.BirthDate ?? normalized.BirthDate;
            if (!ContentAgePolicy.IsValidBirthDate(birthDate, ContentAgePolicy.Today) ||
                (profile.BirthDate is not null && normalized.BirthDate is not null && normalized.BirthDate != profile.BirthDate))
                return "Enter a valid date of birth.";
            profile.BirthDate = birthDate;
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

    /// <summary>Resolves a current content level without disclosing the stored date of birth.</summary>
    public static async Task<ContentPreferences> GetContentAsync(FatewakeDbContext db, Guid? accountId, CancellationToken ct)
    {
        var profile = accountId is { } id
            ? await db.AccountProfiles.AsNoTracking().SingleOrDefaultAsync(row => row.AccountId == id, ct)
            : null;
        return ContentAgePolicy.Resolve(profile?.BirthDate, profile?.ContentLevel, ContentAgePolicy.Today);
    }

    /// <summary>Saves an age-capped preference for the caller and allows a legacy account to provide a missing date once.</summary>
    public async Task<string?> UpdateContentAsync(Guid accountId, ContentPreferenceUpdate update, CancellationToken ct)
    {
        var today = ContentAgePolicy.Today;
        if (update.Level is not { } level || !Enum.IsDefined(level))
            return "Choose a content level.";
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await LockActiveAccountAsync(accountId, ct))
            return "This account is unavailable.";

        var profile = await db.AccountProfiles.SingleOrDefaultAsync(row => row.AccountId == accountId, ct);
        var birthDate = profile?.BirthDate ?? update.BirthDate;
        if (!ContentAgePolicy.IsValidBirthDate(birthDate, today) ||
            (profile?.BirthDate is { } existing && update.BirthDate is { } submitted && existing != submitted))
            return "Enter a valid date of birth.";
        if (level > ContentAgePolicy.Resolve(birthDate, null, today).MaximumLevel)
            return "Choose a content level available for your age.";

        if (profile is null)
        {
            profile = new() { AccountId = accountId };
            db.AccountProfiles.Add(profile);
        }
        profile.BirthDate = birthDate;
        profile.ContentLevel = level;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return null;
    }

    private async Task<bool> LockActiveAccountAsync(Guid accountId, CancellationToken ct)
    {
        var account = await db.Accounts.FromSqlInterpolated(
            $"""SELECT * FROM account WHERE "Id" = {accountId} FOR UPDATE""").SingleOrDefaultAsync(ct);
        return account?.Status == AccountStatus.Active;
    }
}
