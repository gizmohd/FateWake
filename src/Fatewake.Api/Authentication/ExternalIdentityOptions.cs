namespace Fatewake.Api.Authentication;

public sealed class ExternalIdentityOptions
{
    public ProviderOptions Google { get; init; } = new();
    public ProviderOptions Microsoft { get; init; } = new();
    public AppleProviderOptions Apple { get; init; } = new();
}
public class ProviderOptions { public string? ClientId { get; init; } public string? ClientSecret { get; init; } }
public sealed class AppleProviderOptions : ProviderOptions { public string? TeamId { get; init; } public string? KeyId { get; init; } public string? PrivateKey { get; init; } }
