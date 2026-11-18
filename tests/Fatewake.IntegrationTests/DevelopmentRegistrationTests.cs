using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
namespace Fatewake.IntegrationTests;
/// <summary>Verifies automatic registration proof is confined to Development hosting.</summary>
public sealed class DevelopmentRegistrationTests
{
    /// <summary>Development creates a usable verified account without email; other environments retain mailbox proof.</summary>
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public async Task Automatic_verification_is_only_enabled_in_Development(string environment, bool automatic)
    {
        var connection=System.Environment.GetEnvironmentVariable("FATEWAKE_REGISTRATION_TEST_CONNECTION");
        if(string.IsNullOrWhiteSpace(connection)) Assert.Skip("An isolated registration database is required.");
        var ct=TestContext.Current.CancellationToken;
        await using var db=new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync(ct);
        using var mailbox=new TestEmailMailbox();
        var hasher=new PasswordHasher<LocalCredentialRecord>();
        var verification=new VerifiedAccountService(db,mailbox.Sender,NullLogger<VerifiedAccountService>.Instance,hasher,new TestEnvironment(environment));
        var accounts=new LocalAccountService(db,hasher,NullLogger<LocalAccountService>.Instance,verification);
        var email=Guid.NewGuid()+"@test.example";
        const string password="a sufficiently long test password";
        var result=await accounts.RegisterAsync(email,password,ct);
        Assert.Equal(!automatic,result.VerificationRequired);
        if(automatic)
        {
            Assert.NotNull(result.AccountId);
            Assert.True((await db.AccountEmails.SingleAsync(x=>x.AccountId==result.AccountId,ct)).Verified);
            Assert.Equal(result.AccountId,(await accounts.LoginAsync(email,password,ct)).AccountId);
            Assert.False(Directory.Exists(mailbox.DirectoryPath));
            Assert.False(await db.EmailVerifications.AnyAsync(x=>x.Email==email,ct));
            Assert.Null((await accounts.RegisterAsync(email,password,ct)).AccountId);
            Assert.False(await db.Set<AccountRoleRecord>().AnyAsync(x=>x.AccountId==result.AccountId,ct));
        }
        else
        {
            Assert.Null(result.AccountId);
            Assert.Null((await accounts.LoginAsync(email,password,ct)).AccountId);
            Assert.True(Directory.Exists(mailbox.DirectoryPath));
            Assert.NotNull((await verification.VerifyAsync(mailbox.Token(email),ct,password)).AccountId);
        }
    }
    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName {get;set;}=name;
        public string ApplicationName {get;set;}="Fatewake.Tests";
        public string ContentRootPath {get;set;}=".";
        public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();
    }
}
