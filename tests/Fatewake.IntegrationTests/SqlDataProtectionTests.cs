using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies persisted key rings survive provider restarts, isolate applications, and support certificate wrapping.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/SqlDataProtectionTests.md">Documentation</see>
public sealed class SqlDataProtectionTests
{
    /// <summary>Uses the shared SQL ring across independent replicas while keeping application purposes isolated.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Key_ring_survives_provider_restart_and_isolates_applications(bool wrapped)
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "Requires an isolated FATEWAKE_TEST_CONNECTION database.");
        var database = new Npgsql.NpgsqlConnectionStringBuilder(connection)
        {
            Database = "fatewake_keys_" + Guid.NewGuid().ToString("N")
        };
        var options = new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(database.ConnectionString).Options;
        await using var db = new FatewakeDbContext(options);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Fatewake key-ring test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var configuration = new ConfigurationBuilder().Build();
        ServiceProvider Replica(string applicationName)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<FatewakeDbContext>(builder => builder.UseNpgsql(database.ConnectionString));
            var protection = services.AddSqlDataProtection(configuration, applicationName);
            if (wrapped)
                protection.ProtectKeysWithCertificate(certificate).UnprotectKeysWithAnyCertificate(certificate);
            return services.BuildServiceProvider();
        }
        try
        {
            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            string protectedValue;
            using (var first = Replica("Fatewake.Web"))
                protectedValue = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("cookie-test").Protect("test-payload");
            var keys = await db.DataProtectionKeys.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotEmpty(keys);
            if (wrapped)
                Assert.All(keys, key => Assert.Contains("encryptedSecret", key.Xml!));
            using (var restarted = Replica("Fatewake.Web"))
                Assert.Equal("test-payload", restarted.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("cookie-test").Unprotect(protectedValue));
            using var api = Replica("Fatewake.Api");
            Assert.Throws<CryptographicException>(() => api.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("cookie-test").Unprotect(protectedValue));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    /// <summary>Refuses an unwrapped production key ring instead of silently persisting plaintext keys.</summary>
    [Fact]
    public void Production_requires_a_wrapping_certificate()
    {
        var services = new ServiceCollection();
        var error = Assert.Throws<InvalidOperationException>(() =>
            services.AddSqlDataProtection(new ConfigurationBuilder().Build(), "Fatewake.Web"));
        Assert.Contains("CertificateThumbprint", error.Message);
    }
}
