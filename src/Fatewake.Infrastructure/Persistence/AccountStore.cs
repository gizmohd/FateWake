using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

public sealed record ExternalLogin(string Provider,string Subject,string? Email,bool? EmailVerified,string? DisplayName,string? ClaimsJson);
public interface IAccountStore { Task<AccountRecord> ResolveAsync(ExternalLogin login, Guid? guestSurvivorId, CancellationToken ct=default); }
public sealed class AccountStore(FatewakeDbContext db) : IAccountStore
{
    public async Task<AccountRecord> ResolveAsync(ExternalLogin login, Guid? guestSurvivorId, CancellationToken ct=default)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var identity=await db.ExternalIdentities.SingleOrDefaultAsync(x=>x.Provider==login.Provider && x.ProviderSubject==login.Subject,ct);
        AccountRecord account;
        if(identity is not null) { account=await db.Accounts.SingleAsync(x=>x.Id==identity.AccountId,ct); identity.LastLoginAt=DateTimeOffset.UtcNow; }
        else { var now=DateTimeOffset.UtcNow; account=new AccountRecord{Id=Guid.NewGuid(),DisplayName=login.DisplayName,PrimaryEmail=login.Email,Status=AccountStatus.Active,CreatedAt=now,UpdatedAt=now}; db.Accounts.Add(account); db.ExternalIdentities.Add(new ExternalIdentityRecord{Id=Guid.NewGuid(),AccountId=account.Id,Provider=login.Provider,ProviderSubject=login.Subject,Email=login.Email,EmailVerified=login.EmailVerified,DisplayName=login.DisplayName,ClaimsSnapshot=login.ClaimsJson,LinkedAt=now,LastLoginAt=now}); }
        if(guestSurvivorId is { } sid) { var survivor=await db.Survivors.SingleOrDefaultAsync(x=>x.Id==sid,ct); if(survivor is not null && survivor.AccountId is null) survivor.AccountId=account.Id; }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return account;
    }
}
