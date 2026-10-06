using System.Net.Mail;

namespace Fatewake.Infrastructure.Authentication;

/// <summary>Applies identical email validation and case normalization across every identity flow.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/EmailAddressNormalizer.md">Documentation</see>
public static class EmailAddressNormalizer
{
    /// <summary>Validates a plain email address and produces the shared normalized key.</summary>
    public static bool TryNormalize(string? email, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 254) return false;
        var value = email.Trim();
        if (!MailAddress.TryCreate(value, out var address) || address.Address != value || !address.Host.Contains('.')) return false;
        normalized = value.ToUpperInvariant();
        return true;
    }
}
