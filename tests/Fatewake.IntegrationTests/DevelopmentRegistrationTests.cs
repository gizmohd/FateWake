using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Observability;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
namespace Fatewake.IntegrationTests;
/// <summary>Verifies automatic registration proof is confined to Development hosting.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/DevelopmentRegistrationTests.md">Documentation</see>
public sealed class DevelopmentRegistrationTests
{
    /// <summary>Development creates a usable verified account without email; other environments retain mailbox proof.</summary>
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public async Task Automatic_verification_is_only_enabled_in_Development(string environment, bool automatic)
    {
        var connection = System.Environment.GetEnvironmentVariable("FATEWAKE_REGISTRATION_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("An isolated registration database is required.");
        var ct = TestContext.Current.CancellationToken;
        await using var db = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync(ct);
        using var mailbox = new TestEmailMailbox();
        var hasher = new PasswordHasher<LocalCredentialRecord>();
        var verification = new VerifiedAccountService(db, mailbox.Sender, NullLogger<VerifiedAccountService>.Instance, hasher, new TestEnvironment(environment));
        var accounts = new LocalAccountService(db, hasher, NullLogger<LocalAccountService>.Instance, verification);
        var email = Guid.NewGuid() + "@test.example";
        const string password = "a sufficiently long test password";
        var profile = new AccountProfileDetails("  Ana-María ", " O’Neill ", " Montréal ", " Québec ");
        var result = await accounts.RegisterAsync(email, password, ct, profile);
        Assert.Equal(!automatic, result.VerificationRequired);
        if (automatic)
        {
            Assert.NotNull(result.AccountId);
            Assert.True((await db.AccountEmails.SingleAsync(x => x.AccountId == result.AccountId, ct)).Verified);
            Assert.Equal(result.AccountId, (await accounts.LoginAsync(email, password, ct)).AccountId);
            Assert.False(Directory.Exists(mailbox.DirectoryPath));
            Assert.False(await db.EmailVerifications.AnyAsync(x => x.Email == email, ct));
            Assert.Null((await accounts.RegisterAsync(email, password, ct, profile)).AccountId);
            Assert.False(await db.Set<AccountRoleRecord>().AnyAsync(x => x.AccountId == result.AccountId, ct));
        }
        else
        {
            Assert.Null(result.AccountId);
            Assert.Null((await accounts.LoginAsync(email, password, ct)).AccountId);
            Assert.True(Directory.Exists(mailbox.DirectoryPath));
            var pending = await db.EmailVerifications.SingleAsync(x => x.Email == email, ct);
            pending.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
            await db.SaveChangesAsync(ct);
            await verification.ResendAsync(email, ct);
            Assert.NotNull((await verification.VerifyAsync(mailbox.Token(email), ct, password)).AccountId);
            var challenges = await db.EmailVerifications.AsNoTracking().Where(row => row.Email == email).ToListAsync(ct);
            Assert.Equal(2, challenges.Count);
            Assert.All(challenges, row =>
            {
                Assert.Null(row.FirstName);
                Assert.Null(row.LastName);
                Assert.Null(row.City);
                Assert.Null(row.State);
            });
        }
        var owner = await db.Accounts.SingleAsync(row => row.PrimaryEmail == email, ct);
        var persisted = await db.AccountProfiles.AsNoTracking().SingleAsync(row => row.AccountId == owner.Id, ct);
        Assert.Equal(profile.Normalize(), new(persisted.FirstName, persisted.LastName, persisted.City, persisted.State));
        Assert.True(persisted.IsComplete);
        Assert.NotNull(persisted.CompletedAt);
    }

    /// <summary>Every profile field is required and bounded even when browser validation is bypassed.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("line\nbreak")]
    [InlineData("oversized")]
    public async Task Invalid_profile_fields_are_rejected_before_registration(string? invalid)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        using var mailbox = new TestEmailMailbox();
        var accounts = mailbox.Accounts(db, new PasswordHasher<LocalCredentialRecord>());
        for (var index = 0; index < 4; index++)
        {
            string?[] fields = ["Taylor", "Reed", "Bristol", "South West"];
            fields[index] = invalid == "oversized" ? new string('x', 101) : invalid;
            var result = await accounts.RegisterAsync("profile@example.test", "a sufficiently long password", ct,
                new(fields[0], fields[1], fields[2], fields[3]));
            Assert.Equal(AccountProfileDetails.ValidationError, result.Error);
            Assert.Null(result.AccountId);
        }
        Assert.False(await db.Accounts.AnyAsync(ct));
        Assert.False(await db.EmailVerifications.AnyAsync(ct));
        Assert.False(Directory.Exists(mailbox.DirectoryPath));
    }

    /// <summary>Profile completion telemetry uses fixed operation names and never exports submitted personal details.</summary>
    [Fact]
    public async Task Profile_completion_telemetry_does_not_export_personal_details()
    {
        using var parent = new Activity("profile.telemetry.test").Start();
        var spans = new List<Activity>();
        var measurements = new List<KeyValuePair<string, object?>[]>();
        using var activities = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OperationTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == "auth.complete_profile" && activity.ParentSpanId == parent.SpanId) spans.Add(activity);
            }
        };
        ActivitySource.AddActivityListener(activities);
        using var metrics = new MeterListener();
        metrics.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OperationTelemetry.SourceName) listener.EnableMeasurementEvents(instrument);
        };
        metrics.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
        {
            if (Activity.Current?.TraceId == parent.TraceId && instrument.Name == "fatewake.operation.duration" &&
                tags.ToArray().Any(tag => tag.Key == "operation" && Equals(tag.Value, "auth.complete_profile")))
                measurements.Add(tags.ToArray());
        });
        metrics.Start();
        await using var db = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var profiles = new AccountProfileService(db, NullLogger<AccountProfileService>.Instance);
        Assert.Equal(AccountProfileDetails.ValidationError, await profiles.CompleteAsync(Guid.NewGuid(),
            new("Private\nName", "Private Surname", "Private City", "Private Region"), TestContext.Current.CancellationToken));
        Assert.All(Assert.Single(spans).TagObjects, tag => Assert.Equal("operation.outcome", tag.Key));
        Assert.All(Assert.Single(measurements), tag => Assert.Contains(tag.Key, new[] { "operation", "outcome" }));
    }
    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Fatewake.Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
