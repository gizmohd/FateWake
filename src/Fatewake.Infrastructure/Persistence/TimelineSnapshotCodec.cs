using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Validates immutable snapshot integrity and rejects rules or content that this runtime cannot restore.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/TimelineSnapshotCodec.md">Documentation</see>
public static class TimelineSnapshotCodec
{
    /// <summary>Identifies the only deterministic rules currently supported for checkpoint restoration.</summary>
    public const string RulesVersion = "day1-v1";
    /// <summary>Pins scene/beat meaning so changed content is not silently interpreted as an old checkpoint.</summary>
    public const string ContentVersion = "day1-scenes-v1";

    /// <summary>Hashes recursively ordered JSON objects so a PostgreSQL JSONB roundtrip cannot invalidate a checkpoint.</summary>
    public static string Hash(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteCanonical(writer, document.RootElement);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    /// <summary>Rejects corrupt, unsupported or semantically invalid state before any branch rows are created.</summary>
    public static TimelineSnapshot Read(string json, string expectedHash)
    {
        TimelineSnapshot snapshot;
        try
        {
            if (!string.Equals(Hash(json), expectedHash, StringComparison.Ordinal))
                throw new TimelineConflictException("The checkpoint failed its integrity check.");
            snapshot = JsonSerializer.Deserialize<TimelineSnapshot>(json)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new TimelineConflictException("The checkpoint is not a valid snapshot.");
        }
        if (snapshot.Episode is null || snapshot.Panels is null || snapshot.Wakes is null || snapshot.Facts is null ||
            snapshot.SchemaVersion != 1 || snapshot.RulesVersion != RulesVersion ||
            snapshot.ContentVersion != ContentVersion || snapshot.SurvivorDay != 1 ||
            snapshot.Episode.EventKey != "day-001-injured-stranger" ||
            snapshot.Episode.Status != EventInstanceStatus.Active || snapshot.Episode.SurvivorDay != 1 ||
            snapshot.Status != SurvivorStatus.Active)
            throw new TimelineConflictException("This checkpoint requires an unsupported rules or content version.");
        if (string.IsNullOrWhiteSpace(snapshot.Facts) || string.IsNullOrWhiteSpace(snapshot.Episode.State) ||
            snapshot.Panels.Any(panel => panel is null || panel.SourceEventId is not null ||
                panel.TimelineId != snapshot.Episode.TimelineId || panel.SurvivorId != snapshot.Episode.SurvivorId) ||
            snapshot.Wakes.Any(wake => wake is null || wake.Scope != "Personal" || wake.TimelineId != snapshot.Episode.TimelineId))
            throw new TimelineConflictException("The checkpoint contains incompatible personal state.");
        try
        {
            if (JsonSerializer.Deserialize<Dictionary<string, string>>(snapshot.Facts) is null)
                throw new JsonException();
            using var state = JsonDocument.Parse(snapshot.Episode.State);
            if (state.RootElement.ValueKind != JsonValueKind.Object ||
                !state.RootElement.TryGetProperty("sceneKey", out var scene) || scene.ValueKind != JsonValueKind.String ||
                scene.GetString() != "day1-0617-0643" ||
                !state.RootElement.TryGetProperty("beatKey", out var beat) || beat.ValueKind != JsonValueKind.String ||
                beat.GetString() is not ("phone-0617" or "first-choice") ||
                state.RootElement.TryGetProperty("lastOutcome", out var outcome) &&
                outcome.ValueKind != JsonValueKind.Null &&
                (outcome.ValueKind != JsonValueKind.String || !string.IsNullOrWhiteSpace(outcome.GetString())))
                throw new TimelineConflictException("The checkpoint is not a supported pre-decision state.");
        }
        catch (JsonException)
        {
            throw new TimelineConflictException("The checkpoint contains invalid canonical state.");
        }
        return snapshot;
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().OrderBy(row => row.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var value in element.EnumerateArray())
                WriteCanonical(writer, value);
            writer.WriteEndArray();
        }
        else
            element.WriteTo(writer);
    }
}
