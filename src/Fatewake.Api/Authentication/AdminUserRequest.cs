namespace Fatewake.Api.Authentication;

/// <summary>Represents editable canonical account metadata, never a password or verification assertion.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/Authentication/AdminUserRequest.md">Documentation</see>
public sealed record AdminUserRequest(string? Email, string? DisplayName, string? Role, string? Status, DateTimeOffset? Version);
