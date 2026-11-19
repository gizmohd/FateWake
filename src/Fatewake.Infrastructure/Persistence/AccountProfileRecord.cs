using Fatewake.Infrastructure.Authentication;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Stores private registration details separately from survivor identity; an empty row marks a new provider signup awaiting completion.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/AccountProfileRecord.md">Documentation</see>
public sealed class AccountProfileRecord
{
    /// <summary>Canonical owner; never accepted from a profile form.</summary>
    public Guid AccountId
    {
        get; set;
    }
    /// <summary>Self-reported given name, absent only while completing provider signup.</summary>
    public string? FirstName
    {
        get; set;
    }
    /// <summary>Self-reported family name, independent of provider claims and survivor names.</summary>
    public string? LastName
    {
        get; set;
    }
    /// <summary>Self-reported city, not inferred or geocoded.</summary>
    public string? City
    {
        get; set;
    }
    /// <summary>Free-text state or region; no country-specific enumeration is assumed.</summary>
    public string? State
    {
        get; set;
    }
    /// <summary>Last successful profile save; absent for an unfinished signup.</summary>
    public DateTimeOffset? CompletedAt
    {
        get; set;
    }
    /// <summary>Whether all required details satisfy the current registration contract.</summary>
    public bool IsComplete => new AccountProfileDetails(FirstName, LastName, City, State).Normalize() is not null;
}
