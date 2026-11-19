namespace Fatewake.Infrastructure.Authentication;

/// <summary>Validates self-reported account details without restricting names or regions to ASCII or U.S. addresses.</summary>
/// <param name="FirstName">Private given name, independent of survivor identity.</param>
/// <param name="LastName">Private family name, not inferred from provider display names.</param>
/// <param name="City">Self-reported city, never geocoded or used as game-world location.</param>
/// <param name="State">Free-text state or region without a country-specific enumeration.</param>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/AccountProfileDetails.md">Documentation</see>
public sealed record AccountProfileDetails(string? FirstName, string? LastName, string? City, string? State)
{
    /// <summary>Bounds each field consistently across browser, API, and persistence.</summary>
    public const int MaximumLength = 100;
    /// <summary>Safe validation feedback that never echoes submitted personal details.</summary>
    public const string ValidationError = "Enter your first name, last name, city, and state/region, using up to 100 characters for each.";

    /// <summary>Trims surrounding whitespace and rejects missing, oversized, or control-character-bearing fields.</summary>
    public AccountProfileDetails? Normalize()
    {
        var fields = new[] { FirstName, LastName, City, State };
        for (var index = 0; index < fields.Length; index++)
        {
            var value = fields[index]?.Trim();
            if (string.IsNullOrEmpty(value) || value.Length > MaximumLength || value.Any(char.IsControl))
                return null;
            fields[index] = value;
        }
        return new(fields[0], fields[1], fields[2], fields[3]);
    }
}
