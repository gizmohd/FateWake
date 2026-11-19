using System.Globalization;
using System.Security.Cryptography;

namespace Fatewake.Story;

/// <summary>The starting site and world-clock minute assigned to a survivor.</summary>
public sealed record DayOneArrival(string SiteKey, int Minute)
{
    /// <summary>Start of the early morning arrival window.</summary>
    public const int EarliestMinute = 364;
    /// <summary>Latest arrival leaves time for the authored encounter before the broadcast.</summary>
    public const int LatestMinute = 377;

    /// <summary>The opening used by saves created before randomized arrivals.</summary>
    public static DayOneArrival Original { get; } = new("cedar-lane", 377);

    /// <summary>Samples a site and a minute independently from the authored arrival window.</summary>
    public static DayOneArrival Allocate() =>
        new(Sites[RandomNumberGenerator.GetInt32(Sites.Length)].Key,
            RandomNumberGenerator.GetInt32(EarliestMinute, LatestMinute + 1));

    private static readonly Site[] Sites =
    [
        new("cedar-lane", "Cedar Lane", 1,
            "You wake at home on Cedar Lane and try your phone. It shows a signal, but nothing connects.",
            "The lights flicker twice. Car alarms begin answering one another along Cedar Lane.",
            "Across your threshold on Cedar Lane, Michelle is carrying most of an injured man's weight. Eli stands behind her."),
        new("ash-row", "Ash Row", 4,
            "Morning light reaches the duplex on Ash Row. You check your phone, but the first call drops before it rings.",
            "The hall lights pulse. Through the shared wall, you hear a neighbor trying the light switch.",
            "From the duplex porch, you see Michelle supporting an injured man at the foot of the steps. Eli waits on the sidewalk."),
        new("juniper-court", "Juniper Court", 7,
            "You look out from a ground-floor rental on Juniper Court. Your phone shows a signal, but the screen refreshes without fetching anything.",
            "The refrigerator cuts out. On the other side of the courtyard, a car alarm starts.",
            "At the edge of the courtyard, Michelle struggles to support an injured man. Eli keeps close behind her.")
    ];

    private Site Location => Sites.FirstOrDefault(site => site.Key == SiteKey) ?? Sites[0];

    /// <summary>Human-readable starting location.</summary>
    public string SiteName => Location.Name;

    /// <summary>Clock label for the first beat.</summary>
    public string TimeLabel => FormatMinute(Minute);

    /// <summary>Uses stable beat and art keys while adapting the arrival to the survivor's location and clock.</summary>
    public SceneDefinition Opening()
    {
        var scene = DayOneScenes.Opening;
        var location = Location;
        return scene with
        {
            Beats = scene.Beats.Select(beat => beat.Key switch
            {
                "phone-0617" => beat with { Time = TimeLabel, Narration = location.Arrival },
                "house-flicker" => beat with { Time = FormatMinute(Minute + 1), Narration = location.Flicker },
                "street-reveal" => beat with { Time = FormatMinute(Minute + 2), Narration = location.Encounter },
                "first-choice" => beat with { Time = FormatMinute(Minute + 2) },
                "choice-wake" => beat with { Time = FormatMinute(Minute + 3) },
                _ => beat
            }).ToArray()
        };
    }

    /// <summary>Resolves stable authored cells for the interior, threshold, and exterior at the starting site.</summary>
    public Guid CellId(string beatKey, string? outcome)
    {
        var offset = beatKey switch
        {
            "street-reveal" or "first-choice" => 1,
            "choice-wake" when outcome is "injured_stranger_helped" or "helped_maya" => 2,
            "choice-wake" when outcome is "called_from_safety" or "improvised_response" => 1,
            _ => 0
        };
        return Guid.Parse($"10000000-0000-0000-0001-{Location.FirstCell + offset:D12}");
    }

    private static string FormatMinute(int minute) =>
        new TimeOnly(minute / 60, minute % 60).ToString("h:mm tt", CultureInfo.InvariantCulture);

    private sealed record Site(string Key, string Name, int FirstCell, string Arrival, string Flicker, string Encounter);
}
