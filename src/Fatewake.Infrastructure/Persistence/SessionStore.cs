using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

public sealed record SessionState(Guid SurvivorId,Guid TimelineId,Guid EventInstanceId,int SurvivorDay,string EventKey,string Status,string? LastOutcome,IReadOnlyDictionary<string,string> NarrativeFacts,string SceneKey,string BeatKey);

public interface ISessionStore
{
    Task<SessionState> StartOrResumeAsync(Guid? survivorId,string broadRegion,CancellationToken ct=default);
    Task<SessionState?> SavePresentationProgressAsync(Guid survivorId,Guid eventInstanceId,string sceneKey,string beatKey,CancellationToken ct=default);
}

public sealed class SessionStore(FatewakeDbContext db):ISessionStore
{
    public async Task<SessionState> StartOrResumeAsync(Guid? survivorId,string broadRegion,CancellationToken ct=default)
    {
        if(survivorId is {} id)
        {
            var existing=await db.Survivors.SingleOrDefaultAsync(x=>x.Id==id,ct);
            if(existing is not null)
            {
                var existingEpisode=await db.EventInstances.Where(x=>x.SurvivorId==id&&x.Status=="active").OrderBy(x=>x.SurvivorDay).FirstAsync(ct);
                return ToState(existing,existingEpisode);
            }
        }
        var realm=await db.Realms.SingleAsync(x=>x.Key=="the-silence",ct);var now=DateTimeOffset.UtcNow;
        var timeline=new TimelineRecord{Id=Guid.NewGuid(),RealmId=realm.Id,ProgressionState="personal",ConvergenceState="isolated",WorldClockPolicy="activity",CurrentSurvivorDay=1,CreatedAt=now};
        var survivor=new SurvivorRecord{Id=Guid.NewGuid(),TimelineId=timeline.Id,DisplayName="Survivor",IdentityMode="undetermined",BroadRegion=broadRegion,SurvivorDay=1,Status="active",CreatedAt=now,UpdatedAt=now};
        var episode=new EventInstanceRecord{Id=Guid.NewGuid(),TimelineId=timeline.Id,SurvivorId=survivor.Id,EventKey="day-001-injured-stranger",Status="active",SurvivorDay=1,StartedAt=now,State=JsonSerializer.Serialize(new{sceneKey="day1-0617-0643",beatKey="phone-0617"})};
        db.Timelines.Add(timeline);db.Survivors.Add(survivor);db.EventInstances.Add(episode);await db.SaveChangesAsync(ct);return ToState(survivor,episode);
    }

    public async Task<SessionState?> SavePresentationProgressAsync(Guid survivorId,Guid eventInstanceId,string sceneKey,string beatKey,CancellationToken ct=default)
    {
        var survivor=await db.Survivors.SingleOrDefaultAsync(x=>x.Id==survivorId,ct);
        var episode=await db.EventInstances.SingleOrDefaultAsync(x=>x.Id==eventInstanceId&&x.SurvivorId==survivorId,ct);
        if(survivor is null||episode is null)return null;
        var state=ReadState(episode.State);
        episode.State=JsonSerializer.Serialize(new{lastOutcome=state.outcome,narrativeFacts=state.facts,sceneKey,beatKey});
        episode.Version++;await db.SaveChangesAsync(ct);return ToState(survivor,episode);
    }

    private static SessionState ToState(SurvivorRecord survivor,EventInstanceRecord episode)
    {
        var s=ReadState(episode.State);
        return new(survivor.Id,survivor.TimelineId,episode.Id,survivor.SurvivorDay,episode.EventKey,episode.Status,s.outcome,s.facts,s.scene,s.beat);
    }

    private static (string? outcome,IReadOnlyDictionary<string,string> facts,string scene,string beat) ReadState(string? json)
    {
        string? outcome=null;IReadOnlyDictionary<string,string> facts=new Dictionary<string,string>();var scene="day1-0617-0643";var beat="phone-0617";
        if(!string.IsNullOrWhiteSpace(json)&&json!="{}"){using var doc=JsonDocument.Parse(json);var r=doc.RootElement;
            if(r.TryGetProperty("lastOutcome",out var o))outcome=o.GetString();
            if(r.TryGetProperty("narrativeFacts",out var f))facts=JsonSerializer.Deserialize<Dictionary<string,string>>(f.GetRawText())??new();
            if(r.TryGetProperty("sceneKey",out var s)&&!string.IsNullOrWhiteSpace(s.GetString()))scene=s.GetString()!;
            if(r.TryGetProperty("beatKey",out var b)&&!string.IsNullOrWhiteSpace(b.GetString()))beat=b.GetString()!;
        }
        return(outcome,facts,scene,beat);
    }
}
