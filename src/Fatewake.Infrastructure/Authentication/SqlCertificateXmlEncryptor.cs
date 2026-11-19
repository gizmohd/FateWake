using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace Fatewake.Infrastructure.Authentication;

/// <summary>Wraps new Data Protection keys with the currently valid shared SQL certificate.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/SqlCertificateXmlEncryptor.md">Documentation</see>
public sealed class SqlCertificateXmlEncryptor(SqlKeyRingCertificateStore certificates) : IXmlEncryptor
{
    /// <summary>Selects a valid certificate for each new key, allowing rotation without restarting hosts.</summary>
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        using var certificate = certificates.ActiveCertificate();
        return new EncryptedXmlInfo(SqlCertificateCryptography.Encrypt(plaintextElement, certificate), typeof(SqlCertificateXmlDecryptor));
    }
}
