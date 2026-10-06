namespace Fatewake.Api.Authentication;

/// <summary>Sets the first local password for an authenticated external account.</summary>
/// <param name="Password">New password; never logged or returned.</param>
/// <see href="../../../docs/code/src/Fatewake.Api/Authentication/PasswordRequest.md">Documentation</see>
public sealed record PasswordRequest(string? Password);
