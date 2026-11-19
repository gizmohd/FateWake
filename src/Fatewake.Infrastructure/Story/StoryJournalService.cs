using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Story;
using Microsoft.EntityFrameworkCore;
using Fatewake.Observability;
using Microsoft.Extensions.Logging;
namespace Fatewake.Infrastructure.Story;
/// <summary>Stores seen panels and projects history without executing game actions or image generation.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Story/StoryJournalService.md">Documentation</see>
public sealed class StoryJournalService(FatewakeDbContext db, ILogger<StoryJournalService>? log = null)
{
    /// <summary>Records an accepted authoritative decision even if the client disconnects before advancing.</summary>
    public Task RecordOutcomeAsync(SessionState state, CancellationToken ct = default) =>
        RecordBeatAsync(state, (state.Arrival?.Opening() ?? DayOneScenes.Opening).Beat("choice-wake"), false, ct);
    /// <summary>Recovers already-reached legacy beats once, then records the current beat.</summary>
    public async Task RecordProgressAsync(SessionState state, CancellationToken ct = default)
    {
        if (state.EventKey == DayOneScenes.GeneratedOpeningKey)
            return;
        var opening = state.Arrival?.Opening() ?? DayOneScenes.Opening;
        if (state.SceneKey != opening.Key)
            return;
        var beat = opening.Beats.FirstOrDefault(x => x.Key == state.BeatKey);
        if (beat is null)
            return;
        var index = opening.IndexOf(beat.Key);
        if (index > 0)
            foreach (var prior in opening.Beats.Take(index))
                await RecordBeatAsync(state, prior, true, ct);
        await RecordBeatAsync(state, beat, false, ct);
        if (!string.IsNullOrWhiteSpace(state.LastOutcome))
            await RecordBeatAsync(state, opening.Beat("choice-wake"), false, ct);
    }

    private async Task RecordBeatAsync(SessionState state, BeatDefinition beat, bool reconstructed, CancellationToken ct)
    {
        if (beat.Kind == BeatKind.Consequence && string.IsNullOrWhiteSpace(state.LastOutcome))
            return;
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        await LockTimelineAsync(state.SurvivorId, state.TimelineId, ct);
        var key = $"beat:{state.EventInstanceId}:{beat.Key}";
        if (await db.Set<StoryPanelRecord>().AnyAsync(x => x.SurvivorId == state.SurvivorId &&
            x.TimelineId == state.TimelineId && x.EntryKey == key, ct))
            return;
        var layers = DayOneArt.For(beat.ArtworkKey).Select(x => x with { AssetVersion = 0 }).ToArray();
        if (reconstructed)
            for (var i = 0; i < layers.Length; i++)
            {
                var assetKey = layers[i].AssetKey;
                var version = await db.ArtAssets.Where(x => x.Key == assetKey && x.Status == ArtAssetStatus.Approved)
                    .OrderByDescending(x => x.Version).Select(x => (int?)x.Version).FirstOrDefaultAsync(ct);
                layers[i] = layers[i] with
                {
                    AssetVersion = version ?? 0
                };
            }
        db.Add(new StoryPanelRecord
        {
            Id = Guid.NewGuid(),
            SurvivorId = state.SurvivorId,
            TimelineId = state.TimelineId,
            EventInstanceId = state.EventInstanceId,
            EntryKey = key,
            Sequence = await NextSequenceAsync(state.TimelineId, ct),
            SurvivorDay = state.SurvivorDay,
            Kind = beat.Kind == BeatKind.Consequence ? "consequence" : "scene",
            Title = DayOneScenes.Opening.Title,
            TimeLabel = beat.Time,
            Narration = beat.Kind == BeatKind.Consequence ? DayOneOutcomes.Text(state.LastOutcome!, state.NarrativeFacts) : beat.Narration ?? "At the doorway, you decide how to respond to Michelle’s plea.",
            DialogueSpeaker = beat.Dialogue?.Speaker,
            DialogueText = beat.Dialogue?.Text,
            Layers = JsonSerializer.Serialize(layers),
            VisibleFromDay = state.SurvivorDay,
            Reconstructed = reconstructed,
            RecordedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
        if (tx is not null)
            await tx.CommitAsync(ct);
    }

    /// <summary>Pins first displayed approved versions; later regeneration cannot rewrite historical artwork.</summary>
    public async Task<bool> PinArtworkAsync(Guid survivorId, Guid eventInstanceId, string beatKey,
        IReadOnlyDictionary<string, int?> versions, CancellationToken ct = default)
    {
        var key = $"beat:{eventInstanceId}:{beatKey}";
        var panel = await db.Set<StoryPanelRecord>().SingleOrDefaultAsync(x => x.SurvivorId == survivorId && x.EntryKey == key, ct);
        if (panel is null)
            return false;
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        await LockTimelineAsync(panel.SurvivorId, panel.TimelineId, ct);
        await db.Entry(panel).ReloadAsync(ct);
        var layers = JsonSerializer.Deserialize<ArtLayer[]>(panel.Layers) ?? [];
        if (versions.Keys.Any(key => !layers.Any(x => x.AssetKey == key)))
            return false;
        for (var i = 0; i < layers.Length; i++)
            if (layers[i].AssetVersion == 0 && versions.GetValueOrDefault(layers[i].AssetKey) is { } version)
            {
                if (!await db.ArtAssets.AnyAsync(x => x.Key == layers[i].AssetKey && x.Version == version && x.Status == ArtAssetStatus.Approved, ct))
                    return false;
                layers[i] = layers[i] with
                {
                    AssetVersion = version
                };
            }
        panel.Layers = JsonSerializer.Serialize(layers);
        await db.SaveChangesAsync(ct);
        if (tx is not null)
            await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>Reads only reached, audience-owned panels and explicitly revealed cause narration.</summary>
    public async Task<NovelPanel[]> ReadAsync(Guid survivorId, CancellationToken ct = default)
    {
        using var operation = OperationTelemetry.Start("story.read", log);
        try
        {
            var active = await ActiveAsync(survivorId, ct);
            var entries = await db.Set<StoryPanelRecord>().AsNoTracking().Where(x => x.SurvivorId == survivorId &&
                x.TimelineId == active.TimelineId && x.VisibleFromDay <= active.SurvivorDay)
                .OrderBy(x => x.Sequence).ToArrayAsync(ct);
            var level = await ContentLevelAsync(survivorId, ct);
            var panels = entries.Select(x =>
            {
                var narration = VisibleNarration(x);
                var visible = StoryContentPolicy.Allows(level, x.Title) && StoryContentPolicy.Allows(level, x.TimeLabel) &&
                    StoryContentPolicy.Allows(level, narration) && StoryContentPolicy.Allows(level, x.DialogueSpeaker) &&
                    StoryContentPolicy.Allows(level, x.DialogueText);
                return visible
                    ? new NovelPanel(x.Id, x.Sequence, x.SurvivorDay, x.Kind, x.Title, x.TimeLabel, narration,
                        x.DialogueSpeaker, x.DialogueText, JsonSerializer.Deserialize<ArtLayer[]>(x.Layers) ?? [], x.Reconstructed)
                    : new NovelPanel(x.Id, x.Sequence, x.SurvivorDay, "narration", "Hidden journal entry", null,
                        "This entry is unavailable at your current content level.", null, null, [], x.Reconstructed);
            }).ToList();

            if (await db.EventInstances.AnyAsync(row => row.SurvivorId == survivorId && row.TimelineId == active.TimelineId &&
                row.EventKey == DayOneScenes.GeneratedOpeningKey, ct))
                await AddGeneratedPanelsAsync(active, level, panels, ct);
            return panels.ToArray();
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            throw;
        }
    }

    private async Task AddGeneratedPanelsAsync(SurvivorTimelineStateRecord active, ContentLevel level,
        List<NovelPanel> panels, CancellationToken ct)
    {
        var scenes = db.Set<GeneratedSceneRecord>().AsNoTracking()
            .Where(row => row.SurvivorId == active.SurvivorId && row.TimelineId == active.TimelineId);
        var reached = await scenes.Where(row => row.CompletedAt == null).MinAsync(row => (int?)row.Sequence, ct);
        var ready = await scenes.Where(row => row.Status == GeneratedSceneStatus.Ready &&
                (row.CompletedAt != null || row.Sequence == reached))
            .OrderBy(row => row.Sequence).ToArrayAsync(ct);
        var sequence = panels.Count == 0 ? 0 : panels[^1].Sequence;
        foreach (var scene in ready)
        {
            var day = scene.SurvivorDay;
            if (day > active.SurvivorDay)
                continue;
            var spec = StoryPlayService.VisibleSpec(scene, level);
            if (spec is null)
            {
                panels.Add(new(GeneratedPanelId(scene.Id, 0), ++sequence, day, "narration", "Hidden journal entry",
                    null, "This entry is unavailable at your current content level.", null, null, [], false));
                continue;
            }

            for (var index = 0; index < spec.Beats.Count; index++)
            {
                var beat = spec.Beats[index];
                panels.Add(new(GeneratedPanelId(scene.Id, index), ++sequence, day, beat.Kind, spec.Title,
                    spec.TimeLabel, beat.Narration, beat.Speaker, beat.Dialogue, [], false));
            }
        }
    }

    private static Guid GeneratedPanelId(Guid scene, int index) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"generated-scene:{scene:N}:{index}")).AsSpan(0, 16));

    /// <summary>Engine-facing integration for an external user, NPC, group or world event affecting this survivor.</summary>
    /// <remarks>Observable narration must already be approved for this audience. Hidden cause text is never returned until explicitly revealed.</remarks>
    public async Task<Guid> RecordInfluenceAsync(Guid survivorId, Guid sourceEventId, string observableImpact,
        string? hiddenCause, int visibleFromDay, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(observableImpact) || visibleFromDay < 1)
            throw new ArgumentException("Audience-safe impact and visibility day are required.");
        var survivor = await db.Survivors.SingleAsync(x => x.Id == survivorId, ct);
        var source = await db.GameEvents.SingleAsync(x => x.Id == sourceEventId, ct);
        var sourceRealm = await db.Timelines.Where(x => x.Id == source.TimelineId).Select(x => x.RealmId).SingleAsync(ct);
        var targetRealm = await db.Timelines.Where(x => x.Id == survivor.TimelineId).Select(x => x.RealmId).SingleAsync(ct);
        if (sourceRealm != targetRealm)
            throw new InvalidOperationException("Influences must be within the same Realm.");
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        await LockTimelineAsync(survivor.Id, survivor.TimelineId, ct);
        var key = $"influence:{sourceEventId}";
        var existing = await db.Set<StoryPanelRecord>().Where(x => x.SurvivorId == survivorId &&
            x.TimelineId == survivor.TimelineId && x.EntryKey == key).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (existing is { } id)
            return id;
        var panel = new StoryPanelRecord
        {
            Id = Guid.NewGuid(),
            SurvivorId = survivorId,
            TimelineId = survivor.TimelineId,
            SourceEventId = sourceEventId,
            EntryKey = key,
            Sequence = await NextSequenceAsync(survivor.TimelineId, ct),
            SurvivorDay = survivor.SurvivorDay,
            Kind = "cutscene",
            Title = "A turn of fate",
            Narration = observableImpact,
            HiddenCauseNarration = hiddenCause,
            VisibleFromDay = visibleFromDay,
            Layers = "[]",
            RecordedAt = DateTimeOffset.UtcNow,
            NotificationRevision = 1
        };
        db.Add(panel);
        await db.SaveChangesAsync(ct);
        if (tx is not null)
            await tx.CommitAsync(ct);
        return panel.Id;
    }

    /// <summary>Called by authoritative reveal rules when this audience may learn the external cause.</summary>
    public async Task RevealCauseAsync(Guid panelId, CancellationToken ct = default)
    {
        var panel = await db.Set<StoryPanelRecord>().SingleAsync(x => x.Id == panelId && x.Kind == "cutscene", ct);
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        await LockTimelineAsync(panel.SurvivorId, panel.TimelineId, ct);
        await db.Entry(panel).ReloadAsync(ct);
        if (panel.CauseRevealed)
            return;
        panel.CauseRevealed = true;
        if (!string.IsNullOrWhiteSpace(panel.HiddenCauseNarration))
            panel.NotificationRevision++;
        await db.SaveChangesAsync(ct);
        if (tx is not null)
            await tx.CommitAsync(ct);
    }
    /// <summary>Returns pending important updates using the same disclosure gates as the novel.</summary>
    public async Task<StoryNotice[]> NoticesAsync(Guid survivorId, CancellationToken ct = default)
    {
        var active = await ActiveAsync(survivorId, ct);
        var panels = await db.Set<StoryPanelRecord>().AsNoTracking().Where(x => x.SurvivorId == survivorId &&
            x.TimelineId == active.TimelineId && x.VisibleFromDay <= active.SurvivorDay && x.NotificationRevision > x.SeenRevision)
            .OrderBy(x => x.Sequence).Take(10).ToArrayAsync(ct);
        var level = await ContentLevelAsync(survivorId, ct);
        return panels.Select(x =>
        {
            var narration = VisibleNarration(x);
            return new StoryNotice(x.Id, x.NotificationRevision,
                x.CauseRevealed ? "Your story is clearer" : "Your Thread has changed",
                StoryContentPolicy.Allows(level, narration) ? narration : "This update is unavailable at your current content level.");
        }).ToArray();
    }
    private static string VisibleNarration(StoryPanelRecord panel) =>
        panel.CauseRevealed && !string.IsNullOrWhiteSpace(panel.HiddenCauseNarration)
            ? $"{panel.Narration}\n\n{panel.HiddenCauseNarration}" : panel.Narration;

    private async Task<ContentLevel> ContentLevelAsync(Guid survivorId, CancellationToken ct)
    {
        var accountId = await db.Survivors.Where(x => x.Id == survivorId).Select(x => x.AccountId).SingleAsync(ct);
        return (await AccountProfileService.GetContentAsync(db, accountId, ct)).Level;
    }

    /// <summary>Acknowledges only the displayed revision; a later revelation remains unread.</summary>
    public async Task<bool> AcknowledgeAsync(Guid survivorId, Guid panelId, int revision, CancellationToken ct = default)
    {
        var panel = await db.Set<StoryPanelRecord>().SingleOrDefaultAsync(x => x.Id == panelId && x.SurvivorId == survivorId, ct);
        if (panel is null)
            return false;
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        await LockTimelineAsync(panel.SurvivorId, panel.TimelineId, ct);
        await db.Entry(panel).ReloadAsync(ct);
        var active = await ActiveAsync(survivorId, ct);
        if (revision < 1 || revision > panel.NotificationRevision || panel.VisibleFromDay > active.SurvivorDay)
            return false;
        panel.SeenRevision = Math.Max(panel.SeenRevision, revision);
        await db.SaveChangesAsync(ct);
        if (tx is not null)
            await tx.CommitAsync(ct);
        return true;
    }
    private async Task LockTimelineAsync(Guid survivorId, Guid timelineId, CancellationToken ct)
    {
        await new TimelineStateStore(db).LockAsync(survivorId, timelineId, ct);
    }

    private async Task<SurvivorTimelineStateRecord> ActiveAsync(Guid survivorId, CancellationToken ct)
    {
        var survivor = await db.Survivors.AsNoTracking().SingleAsync(row => row.Id == survivorId, ct);
        return await db.Set<SurvivorTimelineStateRecord>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.SurvivorId == survivorId && row.TimelineId == survivor.TimelineId, ct)
            ?? new SurvivorTimelineStateRecord
            {
                SurvivorId = survivorId,
                TimelineId = survivor.TimelineId,
                SurvivorDay = survivor.SurvivorDay,
                Status = survivor.Status
            };
    }
    private async Task<long> NextSequenceAsync(Guid timelineId, CancellationToken ct) =>
        (await db.Set<StoryPanelRecord>().Where(x => x.TimelineId == timelineId).MaxAsync(x => (long?)x.Sequence, ct) ?? 0) + 1;
}
