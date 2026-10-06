namespace Fatewake.Api.Authentication;

/// <summary>Requests another verification email without disclosing account existence.</summary>
/// <param name="Email">Destination account email.</param>
/// <see href="../../../docs/code/src/Fatewake.Api/Authentication/EmailRequest.md">Documentation</see>
public sealed record EmailRequest(string? Email);
