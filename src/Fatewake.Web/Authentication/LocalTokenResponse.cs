namespace Fatewake.Web.Authentication;

/// <summary>Receives the API's protected bearer session without exposing it to browser JavaScript.</summary>
/// <param name="AccessToken">Opaque API bearer token.</param>
/// <param name="ExpiresIn">Session lifetime in seconds.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Authentication/LocalTokenResponse.md">LocalTokenResponse documentation</see>
public sealed record LocalTokenResponse(string AccessToken, long ExpiresIn);
