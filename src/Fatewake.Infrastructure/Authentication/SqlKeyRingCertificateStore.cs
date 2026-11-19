using Fatewake.Infrastructure.Persistence;
using Fatewake.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace Fatewake.Infrastructure.Authentication;

/// <summary>Coordinates shared certificate issuance and safe retirement through PostgreSQL transactions.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/SqlKeyRingCertificateStore.md">Documentation</see>
public sealed class SqlKeyRingCertificateStore(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<SqlKeyRingCertificateStore> logger, string password)
{
    /// <summary>Returns a caller-owned certificate valid for new envelopes; replicas serialize issuance through a database lock.</summary>
    public X509Certificate2 ActiveCertificate()
    {
        using var operation = OperationTelemetry.Start("data_protection.certificate_select", logger);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
            using var transaction = db.Database.BeginTransaction();
            db.Database.ExecuteSqlRaw("SELECT pg_advisory_xact_lock(861017236)");
            var row = Active(db, clock.GetUtcNow());
            transaction.Commit();
            return Load(row);
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            logger.LogError("Data Protection certificate selection failed with {ExceptionType}", ex.GetType().FullName);
            throw;
        }
    }

    /// <summary>Loads an encrypted private certificate for a historical envelope; never falls back to another certificate.</summary>
    public X509Certificate2 ReadCertificate(string thumbprint)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
        var row = db.DataProtectionCertificates.AsNoTracking().SingleOrDefault(record => record.Thumbprint == thumbprint)
            ?? throw new CryptographicException("The Data Protection wrapping certificate is not available.");
        return Load(row);
    }

    /// <summary>Rewraps ring entries before retiring certificates expired for 90 days; a one-day grace protects concurrent readers.</summary>
    public void Maintain(CancellationToken ct = default)
    {
        using var operation = OperationTelemetry.Start("data_protection.certificate_maintain", logger);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
            using var transaction = db.Database.BeginTransaction();
            db.Database.ExecuteSqlRaw("SELECT pg_advisory_xact_lock(861017236)");
            ct.ThrowIfCancellationRequested();
            var now = clock.GetUtcNow();
            using var active = Load(Active(db, now));
            var expired = db.DataProtectionCertificates.Where(row => row.NotAfter <= now.AddDays(-90)).ToList();
            var expiredById = expired.ToDictionary(row => row.Thumbprint, StringComparer.Ordinal);
            var rewrapped = 0;
            if (expired.Count != 0)
            {
                foreach (var key in db.DataProtectionKeys.ToList())
                {
                    ct.ThrowIfCancellationRequested();
                    if (key.Xml is null) continue;
                    var xml = XElement.Parse(key.Xml);
                    var changed = false;
                    foreach (var envelope in xml.Descendants("sqlEncryptedKey").ToArray())
                    {
                        if ((string?)envelope.Attribute("certificate") is not { } thumbprint ||
                            !expiredById.TryGetValue(thumbprint, out var old)) continue;
                        using var previous = Load(old);
                        envelope.ReplaceWith(SqlCertificateCryptography.Encrypt(SqlCertificateCryptography.Decrypt(envelope, previous), active));
                        changed = true;
                    }
                    if (!changed) continue;
                    key.Xml = xml.ToString(SaveOptions.DisableFormatting);
                    rewrapped++;
                }
                foreach (var row in expired)
                    row.RewrappedAt ??= now;
            }
            var removed = expired.Where(row => row.RewrappedAt <= now.AddDays(-1)).ToArray();
            db.DataProtectionCertificates.RemoveRange(removed);
            db.SaveChanges();
            transaction.Commit();
            if (rewrapped != 0 || removed.Length != 0)
                logger.LogInformation("Data Protection maintenance rewrapped {KeyCount} keys and removed {CertificateCount} retired certificates",
                    rewrapped, removed.Length);
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            logger.LogError("Data Protection certificate maintenance failed with {ExceptionType}", ex.GetType().FullName);
            throw;
        }
    }

    private DataProtectionCertificateRecord Active(FatewakeDbContext db, DateTimeOffset now)
    {
        var row = db.DataProtectionCertificates.Where(record => record.NotBefore <= now && record.NotAfter > now.AddMinutes(5))
            .OrderByDescending(record => record.NotAfter).FirstOrDefault();
        if (row is not null) return row;
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=Fatewake Data Protection", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, critical: true));
        using var certificate = request.CreateSelfSigned(now, now.AddDays(7));
        row = new DataProtectionCertificateRecord
        {
            Thumbprint = certificate.Thumbprint,
            EncryptedPfx = certificate.ExportPkcs12(new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100000), password),
            NotBefore = new DateTimeOffset(certificate.NotBefore.ToUniversalTime()),
            NotAfter = new DateTimeOffset(certificate.NotAfter.ToUniversalTime())
        };
        db.DataProtectionCertificates.Add(row);
        db.SaveChanges();
        logger.LogInformation("Created shared Data Protection wrapping certificate valid until {ExpiresAt}", row.NotAfter);
        return row;
    }

    private X509Certificate2 Load(DataProtectionCertificateRecord row) =>
        X509CertificateLoader.LoadPkcs12(row.EncryptedPfx, password, X509KeyStorageFlags.EphemeralKeySet);
}
