using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies persisted key rings survive provider restarts, isolate applications, and support certificate wrapping.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/SqlDataProtectionTests.md">Documentation</see>
public sealed class SqlDataProtectionTests
{
    /// <summary>Uses the shared SQL ring across independent replicas while keeping application purposes isolated.</summary>
    [Theory]
    [InlineData("Fatewake.Web")]
    [InlineData("Fatewake.Api")]
    public async Task Key_ring_survives_provider_restart_and_isolates_applications(string applicationName)
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "Requires an isolated FATEWAKE_TEST_CONNECTION database.");
        var database = new Npgsql.NpgsqlConnectionStringBuilder(connection)
        {
            Database = "fatewake_keys_" + Guid.NewGuid().ToString("N")
        };
        var options = new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(database.ConnectionString).Options;
        await using var db = new FatewakeDbContext(options);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DataProtection:CertificatePassword"] = "Disposable SQL key-ring regression fixture password"
        }).Build();
        ServiceProvider Replica(string name, bool readOnly = false)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<FatewakeDbContext>(builder => builder.UseNpgsql(database.ConnectionString));
            var protection = services.AddSqlDataProtection(configuration, name);
            if (readOnly)
                protection.DisableAutomaticKeyGeneration();
            return services.BuildServiceProvider();
        }
        try
        {
            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            string protectedValue;
            using (var first = Replica(applicationName))
                protectedValue = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("cookie-test").Protect("test-payload");
            var keys = await db.DataProtectionKeys.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotEmpty(keys);
            Assert.All(keys, key => Assert.Contains("SqlCertificateXmlDecryptor", key.Xml!));
            Assert.All(keys, key => Assert.Contains("sqlEncryptedKey", key.Xml!));
            var certificateCount = await db.DataProtectionCertificates.CountAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, certificateCount);
            using (var restarted = Replica(applicationName, readOnly: true))
                Assert.Equal("test-payload", restarted.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("cookie-test").Unprotect(protectedValue));
            using var otherApplication = Replica(applicationName == "Fatewake.Web" ? "Fatewake.Api" : "Fatewake.Web", readOnly: true);
            Assert.Throws<CryptographicException>(() => otherApplication.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("cookie-test").Unprotect(protectedValue));
            var after = await db.DataProtectionKeys.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
            Assert.True(keys.OrderBy(key => key.Id).Select(key => key.Xml).SequenceEqual(
                after.OrderBy(key => key.Id).Select(key => key.Xml)), "Reading the persisted ring must not replace or rewrite existing keys.");
            Assert.Equal(certificateCount, await db.DataProtectionCertificates.CountAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    /// <summary>Refuses explicitly empty or weak wrapping passwords instead of silently persisting plaintext keys.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public void Invalid_wrapping_password_is_rejected(string password)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DataProtection:CertificatePassword"] = password
        }).Build();
        var error = Assert.Throws<InvalidOperationException>(() => services.AddSqlDataProtection(configuration, "Fatewake.Web"));
        Assert.Contains("CertificatePassword", error.Message);
    }
}
