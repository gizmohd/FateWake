namespace Fatewake.Api.Authentication;

/// <summary>Contains OAuth client credentials for an identity provider.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/Authentication/ProviderOptions.md">ProviderOptions documentation</see>
public class ProviderOptions
{
    /// <summary>Registers provider sign-in only when explicitly enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Public client identifier registered with the provider.</summary>
    public string? ClientId { get; init; }

    /// <summary>Client secret registered with the provider.</summary>
    public string? ClientSecret { get; init; }

    /// <summary>Microsoft tenant GUID or consumers for personal accounts; ignored by Google.</summary>
    public string TenantId { get; init; } = "consumers";
}
