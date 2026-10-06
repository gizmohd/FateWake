using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

public sealed record SessionState(Guid SurvivorId, Guid TimelineId, Guid EventInstanceId, int SurvivorDay, string EventKey, string Status);

public interface ISessionStore
{
    Task<SessionState> StartOrResumeAsync(Guid? survivorId, string broadRegion, CancellationToken ct = default);
}

public sealed class SessionStore(FatewakeDbContext db) : ISessionStore
{
    public async Task<SessionState> StartOrResumeAsync(Guid? survivorId, string broadRegion, CancellationToken ct = default)
    {
        if (survivorId is { } id)
        {
            var existing = await db.Survivors.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (existing is not null)
            {
                var episode = await db.EventInstances.Where(x => x.SurvivorId == id && x.Status == "active").OrderBy(x => x.SurvivorDay).FirstAsync(ct);
                return new(existing.Id, existing.TimelineId, episode.Id, existing.SurvivorDay, episode.EventKey, episode.Status);
            }
        }

        var realm = await db.Realms.SingleAsync(x => x.Key == "the-silence", ct);
        var now = DateTimeOffset.UtcNow;
        var timeline = new TimelineRecord { Id = Guid.NewGuid(), RealmId = realm.Id, ProgressionState = "personal", ConvergenceState = "isolated", WorldClockPolicy = "activity", CurrentSurvivorDay = 1, CreatedAt = now };
        var survivor = new SurvivorRecord { Id = Guid.NewGuid(), TimelineId = timeline.Id, DisplayName = "Survivor", IdentityMode = "undetermined", BroadRegion = broadRegion, SurvivorDay = 1, Status = "active", CreatedAt = now, UpdatedAt = now };
        var episode = new EventInstanceRecord { Id = Guid.NewGuid(), TimelineId = timeline.Id, SurvivorId = survivor.Id, EventKey = "day-001-injured-stranger", Status = "active", SurvivorDay = 1, StartedAt = now };
        db.Timelines.Add(timeline); db.Survivors.Add(survivor); db.EventInstances.Add(episode);
        await db.SaveChangesAsync(ct);
        return new(survivor.Id, timeline.Id, episode.Id, 1, episode.EventKey, episode.Status);
    }
}
