namespace Fatewake.Infrastructure.Persistence;

/// <summary>Provider claims supplied only after cryptographic validation of the provider token.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/ExternalLogin.md">Documentation</see>
public sealed record ExternalLogin(string Provider, string Subject, string? Email, bool? EmailVerified, string? DisplayName, string? ClaimsJson);
