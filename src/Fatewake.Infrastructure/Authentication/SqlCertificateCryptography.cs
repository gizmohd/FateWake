using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Fatewake.Infrastructure.Authentication;

/// <summary>Uses authenticated AES-256-GCM envelopes with RSA-OAEP-SHA256 certificate key wrapping.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/SqlCertificateCryptography.md">Documentation</see>
internal static class SqlCertificateCryptography
{
    internal static XElement Encrypt(XElement plaintext, X509Certificate2 certificate)
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var bytes = Encoding.UTF8.GetBytes(plaintext.ToString(SaveOptions.DisableFormatting));
        try
        {
            var ciphertext = new byte[bytes.Length];
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, bytes, ciphertext, tag, Encoding.ASCII.GetBytes(certificate.Thumbprint));
            using var rsa = certificate.GetRSAPublicKey() ?? throw new CryptographicException("Data Protection certificate has no RSA public key.");
            return new XElement("sqlEncryptedKey",
                new XAttribute("certificate", certificate.Thumbprint),
                new XAttribute("version", "1"),
                new XElement("wrappedKey", Convert.ToBase64String(rsa.Encrypt(key, RSAEncryptionPadding.OaepSHA256))),
                new XElement("nonce", Convert.ToBase64String(nonce)),
                new XElement("tag", Convert.ToBase64String(tag)),
                new XElement("ciphertext", Convert.ToBase64String(ciphertext)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    internal static XElement Decrypt(XElement encrypted, X509Certificate2 certificate)
    {
        if (encrypted.Name != "sqlEncryptedKey" || (string?)encrypted.Attribute("version") != "1" ||
            (string?)encrypted.Attribute("certificate") != certificate.Thumbprint)
            throw new CryptographicException("Unsupported or mismatched Data Protection envelope.");
        using var rsa = certificate.GetRSAPrivateKey() ?? throw new CryptographicException("Data Protection certificate has no RSA private key.");
        var key = rsa.Decrypt(Convert.FromBase64String(Required(encrypted, "wrappedKey")), RSAEncryptionPadding.OaepSHA256);
        var ciphertext = Convert.FromBase64String(Required(encrypted, "ciphertext"));
        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(Convert.FromBase64String(Required(encrypted, "nonce")), ciphertext,
                Convert.FromBase64String(Required(encrypted, "tag")), plaintext, Encoding.ASCII.GetBytes(certificate.Thumbprint));
            using var reader = XmlReader.Create(new MemoryStream(plaintext, writable: false),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
            return XElement.Load(reader);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static string Required(XElement element, string name) =>
        element.Element(name)?.Value ?? throw new CryptographicException("Data Protection envelope is incomplete.");
}
