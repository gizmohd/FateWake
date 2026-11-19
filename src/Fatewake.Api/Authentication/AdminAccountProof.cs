namespace Fatewake.Api.Authentication;

/// <summary>Materializes non-secret challenge metadata after looking up a hashed proof.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/Authentication/AdminAccountProof.md">Documentation</see>
internal sealed record AdminAccountProof(Guid AccountId, string Purpose, string Email, string NormalizedEmail);
