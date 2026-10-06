namespace Fatewake.Api.Authentication;

/// <summary>Accepts email and password for local registration or login.</summary>
/// <param name="Email">Email address.</param>
/// <param name="Password">Password; never included in logs or responses.</param>
/// <see href="../../../docs/code/src/Fatewake.Api/Authentication/LocalLoginRequest.md">LocalLoginRequest documentation</see>
public sealed record LocalLoginRequest(string? Email, string? Password);
