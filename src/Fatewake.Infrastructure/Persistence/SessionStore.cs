using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Creates or resumes guest/account-owned survivors and persists presentation progress.</summary>
/// <param name="db">Scoped canonical PostgreSQL persistence context.</param>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/SessionStore.md">SessionStore documentation</see>
public sealed class SessionStore(FatewakeDbContext db):ISessionStore
{
    /// <inheritdoc />
    public async Task<SessionState> StartOrResumeAsync(Guid? survivorId,string broadRegion,CancellationToken ct=default,Guid? accountId=null)
    {
        await using var transaction = accountId is not null ? await db.Database.BeginTransactionAsync(ct) : null;
        if (accountId is {} owner)
        {
            var account = await db.Accounts.FromSqlInterpolated($"""SELECT * FROM account WHERE "Id" = {owner} FOR UPDATE""").SingleAsync(ct);
            if (account.Status != AccountStatus.Active) throw new UnauthorizedAccessException("The account is not active.");
        }
        if (survivorId is null && accountId is not null)
            survivorId = await db.Survivors.Where(x => x.AccountId == accountId && x.Status == SurvivorStatus.Active)
                .OrderBy(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if(survivorId is {} id)
        {
            var existing=await db.Survivors.SingleOrDefaultAsync(x=>x.Id==id&&x.AccountId==accountId,ct);
            if(existing is not null)
            {
                var existingEpisode=await db.EventInstances.Where(x=>x.SurvivorId==id&&x.Status==EventInstanceStatus.Active).OrderBy(x=>x.SurvivorDay).FirstAsync(ct);
                return ToState(existing,existingEpisode);
            }
        }
        var realm=await db.Realms.SingleAsync(x=>x.Key=="the-silence",ct);var now=DateTimeOffset.UtcNow;
        var timeline=new TimelineRecord{Id=Guid.NewGuid(),RealmId=realm.Id,ProgressionState="personal",ConvergenceState="isolated",WorldClockPolicy="activity",CurrentSurvivorDay=1,CreatedAt=now};
        var survivor=new SurvivorRecord{Id=Guid.NewGuid(),AccountId=accountId,TimelineId=timeline.Id,DisplayName="Survivor",IdentityMode="undetermined",BroadRegion=broadRegion,SurvivorDay=1,Status=SurvivorStatus.Active,CreatedAt=now,UpdatedAt=now};
        var episode=new EventInstanceRecord{Id=Guid.NewGuid(),TimelineId=timeline.Id,SurvivorId=survivor.Id,EventKey="day-001-injured-stranger",Status=EventInstanceStatus.Active,SurvivorDay=1,StartedAt=now,State=JsonSerializer.Serialize(new{sceneKey="day1-0617-0643",beatKey="phone-0617"})};
        db.Timelines.Add(timeline);db.Survivors.Add(survivor);db.EventInstances.Add(episode);await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return ToState(survivor,episode);
    }

    /// <inheritdoc />
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
        return new(survivor.Id,survivor.TimelineId,episode.Id,survivor.SurvivorDay,episode.EventKey,episode.Status.ToString().ToLowerInvariant(),s.outcome,s.facts,s.scene,s.beat);
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
