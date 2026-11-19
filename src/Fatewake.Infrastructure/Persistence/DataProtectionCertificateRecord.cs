namespace Fatewake.Infrastructure.Persistence;

/// <summary>Stores an encrypted private certificate shared by Data Protection replicas.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/DataProtectionCertificateRecord.md">Documentation</see>
public sealed class DataProtectionCertificateRecord
{
    public string Thumbprint { get; set; } = "";
    public byte[] EncryptedPfx { get; set; } = [];
    public DateTimeOffset NotBefore { get; set; }
    public DateTimeOffset NotAfter { get; set; }
    public DateTimeOffset? RewrappedAt { get; set; }
}
