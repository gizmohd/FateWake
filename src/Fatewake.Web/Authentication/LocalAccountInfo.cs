namespace Fatewake.Web.Authentication;

/// <summary>Receives canonical account identity from the authenticated API.</summary>
/// <param name="AccountId">Canonical account identifier.</param>
/// <param name="Email">Account email.</param>
/// <param name="HasPassword">Whether local login is configured.</param>
/// <param name="EmailVerified">Whether mailbox ownership has been proven.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Authentication/LocalAccountInfo.md">LocalAccountInfo documentation</see>
public sealed record LocalAccountInfo(Guid AccountId, string Email, bool HasPassword, bool EmailVerified);
