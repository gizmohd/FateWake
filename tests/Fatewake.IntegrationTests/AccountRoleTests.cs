using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Fatewake.IntegrationTests;
/// <summary>Checks the initial Admin assignment against PostgreSQL under concurrent sign-ins.</summary>
public sealed class AccountRoleTests
{
    /// <summary>Registration order does not decide Admin; the first sign-in does, and concurrent later accounts stay User.</summary>
    [Fact]
    public async Task First_sign_in_is_Admin_and_all_other_accounts_are_User()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_ROLE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("An isolated PostgreSQL role test database is required.");
        var options = new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(connection).Options;
        var ct = TestContext.Current.CancellationToken;
        var accounts = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToArray();
        await using (var db = new FatewakeDbContext(options))
        {
            await db.Database.MigrateAsync(ct);
            foreach (var id in accounts)
            {
                db.Accounts.Add(new AccountRecord { Id=id, Status=AccountStatus.Active, CreatedAt=DateTimeOffset.UtcNow, UpdatedAt=DateTimeOffset.UtcNow });
                db.AccountEmails.Add(new AccountEmailRecord { AccountId=id, NormalizedEmail=id+"@TEST.EXAMPLE", Verified=true });
            }
            await db.SaveChangesAsync(ct);
        }
        // The last registered account is the first to authenticate.
        await using (var db = new FatewakeDbContext(options)) Assert.Equal("Admin",await new AccountRoleService(db).AssignOnSignInAsync(accounts[^1],ct));
        var roles = await Task.WhenAll(accounts.Select(async id => {
            await using var db = new FatewakeDbContext(options);
            return await new AccountRoleService(db).AssignOnSignInAsync(id,ct);
        }));
        Assert.Single(roles,x=>x=="Admin"); Assert.Equal(11,roles.Count(x=>x=="User"));
        await using (var db = new FatewakeDbContext(options))
        {
            Assert.Equal(12,await db.Set<AccountRoleRecord>().CountAsync(ct));
            Assert.Equal("Admin",await new AccountRoleService(db).AssignOnSignInAsync(accounts[^1],ct));
        }
    }
    /// <summary>Simultaneous first sign-ins produce exactly one durable Admin.</summary>
    [Fact]
    public async Task Concurrent_initial_sign_ins_choose_exactly_one_Admin()
    {
        var connection=Environment.GetEnvironmentVariable("FATEWAKE_ROLE_RACE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("An isolated PostgreSQL race test database is required.");
        var options=new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(connection).Options;
        var ct=TestContext.Current.CancellationToken; var ids=Enumerable.Range(0,8).Select(_=>Guid.NewGuid()).ToArray();
        await using (var db=new FatewakeDbContext(options))
        {
            await db.Database.MigrateAsync(ct);
            foreach(var id in ids) { db.Accounts.Add(new AccountRecord { Id=id,Status=AccountStatus.Active,CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow }); db.AccountEmails.Add(new AccountEmailRecord { AccountId=id,NormalizedEmail=id+"@TEST.EXAMPLE",Verified=true }); }
            await db.SaveChangesAsync(ct);
        }
        var results=await Task.WhenAll(ids.Select(async id=>{ await using var db=new FatewakeDbContext(options); return await new AccountRoleService(db).AssignOnSignInAsync(id,ct); }));
        Assert.Single(results,x=>x=="Admin"); Assert.Equal(7,results.Count(x=>x=="User"));
    }
}

