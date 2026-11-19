using System.Xml.Linq;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace Fatewake.Infrastructure.Authentication;

/// <summary>Resolves the exact historical SQL certificate recorded by a Data Protection envelope.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/SqlCertificateXmlDecryptor.md">Documentation</see>
public sealed class SqlCertificateXmlDecryptor(SqlKeyRingCertificateStore certificates) : IXmlDecryptor
{
    /// <summary>Authenticates and decrypts one envelope without exposing private keys or plaintext through logging.</summary>
    public XElement Decrypt(XElement encryptedElement)
    {
        var thumbprint = (string?)encryptedElement.Attribute("certificate")
            ?? throw new CryptographicException("Data Protection envelope has no certificate identifier.");
        using var certificate = certificates.ReadCertificate(thumbprint);
        return SqlCertificateCryptography.Decrypt(encryptedElement, certificate);
    }
}
