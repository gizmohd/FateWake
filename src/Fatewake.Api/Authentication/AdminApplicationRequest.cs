namespace Fatewake.Api.Authentication;

/// <summary>Defines the editable settings for an application using the server's authorization-code flow.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/Authentication/AdminApplicationRequest.md">Documentation</see>
public sealed record AdminApplicationRequest(string? ClientId, string? DisplayName, string? ClientType,
    string[]? RedirectUris, string[]? PostLogoutRedirectUris, bool AllowRefreshTokens, string? Version);
