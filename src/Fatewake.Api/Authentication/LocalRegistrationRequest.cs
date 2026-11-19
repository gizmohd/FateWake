namespace Fatewake.Api.Authentication;

/// <summary>Accepts credentials and required private profile details for a new local account.</summary>
/// <param name="Email">Mailbox whose ownership must be proven.</param>
/// <param name="Password">Password to hash; never logged or returned.</param>
/// <param name="FirstName">Self-reported given name.</param>
/// <param name="LastName">Self-reported family name.</param>
/// <param name="City">Self-reported city.</param>
/// <param name="State">Free-text state or region.</param>
/// <see href="../../../docs/code/src/Fatewake.Api/Authentication/LocalRegistrationRequest.md">Documentation</see>
public sealed record LocalRegistrationRequest(string? Email, string? Password, string? FirstName, string? LastName, string? City, string? State);
