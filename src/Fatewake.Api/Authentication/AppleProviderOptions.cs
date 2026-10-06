namespace Fatewake.Api.Authentication;

/// <summary>Contains the additional signing credentials required for Apple identity integration.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/Authentication/AppleProviderOptions.md">AppleProviderOptions documentation</see>
public sealed class AppleProviderOptions : ProviderOptions
{
    /// <summary>Apple developer team identifier.</summary>
    public string? TeamId { get; init; }

    /// <summary>Apple sign-in key identifier.</summary>
    public string? KeyId { get; init; }

    /// <summary>Private signing key material supplied through secure configuration.</summary>
    public string? PrivateKey { get; init; }
}
