namespace Fatewake.Api.Authentication;

/// <summary>Accepts an email-verification token for explicit single-use confirmation.</summary>
/// <param name="Token">Opaque token delivered to the mailbox.</param>
/// <param name="Password">Registration password confirmation; not needed for external mailbox proof.</param>
/// <see href="../../../docs/code/src/Fatewake.Api/Authentication/VerificationRequest.md">Documentation</see>
public sealed record VerificationRequest(string? Token, string? Password = null);
