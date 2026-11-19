namespace Fatewake.Web.Authentication;

/// <summary>Receives the API's protected bearer session without exposing it to browser JavaScript.</summary>
/// <param name="AccessToken">Opaque API bearer token.</param>
/// <param name="ExpiresIn">Session lifetime in seconds.</param>
/// <param name="RefreshToken">Optional protected refresh token.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Authentication/LocalTokenResponse.md">LocalTokenResponse documentation</see>
public sealed record LocalTokenResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
    [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] long ExpiresIn,
    [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string? RefreshToken = null);
