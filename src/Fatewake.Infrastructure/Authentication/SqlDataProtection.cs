using Fatewake.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Authentication;

/// <summary>Stores Data Protection keys in PostgreSQL, separate from OpenIddict signing/encryption credentials.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/SqlDataProtection.md">Documentation</see>
public static class SqlDataProtection
{
    /// <summary>Shares an automatically certificate-wrapped ring while keeping the private-certificate password outside SQL.</summary>
    public static IDataProtectionBuilder AddSqlDataProtection(this IServiceCollection services, IConfiguration configuration,
        string applicationName)
    {
        var password = configuration["DataProtection:CertificatePassword"] ?? "FateWake2026!@#123B9A9AFC364FA47F98D80075D29A288BF}";
        if (string.IsNullOrWhiteSpace(password) || password.Length < 32)
            throw new InvalidOperationException("Configure the same DataProtection:CertificatePassword of at least 32 characters on every Web/API replica. Do not store it in SQL or source control.");
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(provider => new SqlKeyRingCertificateStore(provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<ILogger<SqlKeyRingCertificateStore>>(), password));
        services.AddOptions<KeyManagementOptions>().Configure<SqlKeyRingCertificateStore>((options, certificates) =>
            options.XmlEncryptor = new SqlCertificateXmlEncryptor(certificates));
        services.AddHostedService<SqlCertificateMaintenanceWorker>();
        var protection = services.AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToDbContext<FatewakeDbContext>();
        return protection;
    }
}
