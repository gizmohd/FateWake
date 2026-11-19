namespace Fatewake.Infrastructure.Persistence;
/// <summary>Canonical role assigned when an account first completes sign-in.</summary>
public sealed class AccountRoleRecord
{
    public Guid AccountId
    {
        get; set;
    }
    public required string Role
    {
        get; set;
    }
    public DateTimeOffset AssignedAt
    {
        get; set;
    }
}
