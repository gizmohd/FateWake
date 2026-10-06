namespace Fatewake.Api.Authentication;

/// <summary>Contains OAuth client credentials for an identity provider.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/Authentication/ProviderOptions.md">ProviderOptions documentation</see>
public class ProviderOptions
{
    /// <summary>Public client identifier registered with the provider.</summary>
    public string? ClientId { get; init; }

    /// <summary>Client secret registered with the provider.</summary>
    public string? ClientSecret { get; init; }
}
