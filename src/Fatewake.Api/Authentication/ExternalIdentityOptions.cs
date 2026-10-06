namespace Fatewake.Api.Authentication;

/// <summary>Configuration for supported external identity providers.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/Authentication/ExternalIdentityOptions.md">ExternalIdentityOptions documentation</see>
public sealed class ExternalIdentityOptions
{
    /// <summary>OAuth client configuration for Google sign-in.</summary>
    public ProviderOptions Google { get; init; } = new();

    /// <summary>OAuth client configuration for Microsoft sign-in.</summary>
    public ProviderOptions Microsoft { get; init; } = new();

    /// <summary>OAuth client configuration for Apple sign-in.</summary>
    public AppleProviderOptions Apple { get; init; } = new();
}
