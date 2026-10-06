namespace Fatewake.Infrastructure.Authentication;

/// <summary>Represents an authenticated local account or a safe validation/login error.</summary>
/// <param name="AccountId">Authenticated account identifier, or null for failure.</param>
/// <param name="Email">Canonical email for a successful result.</param>
/// <param name="Error">Safe player-facing failure message.</param>
/// <param name="VerificationRequired">Mailbox proof is required before issuing a session.</param>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/LocalAccountResult.md">LocalAccountResult documentation</see>
public sealed record LocalAccountResult(Guid? AccountId, string? Email, string? Error, bool VerificationRequired = false);
