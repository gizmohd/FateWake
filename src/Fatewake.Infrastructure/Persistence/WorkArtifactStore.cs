using Microsoft.EntityFrameworkCore;
namespace Fatewake.Infrastructure.Persistence;
/// <summary>Implements PostgreSQL-backed job artifact exchange with unique job/key identity.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/WorkArtifactStore.md">WorkArtifactStore documentation</see>.</remarks>
public sealed class WorkArtifactStore(FatewakeDbContext db):IWorkArtifactStore
{
    public Task<string?> GetAsync(Guid jobId,string key,CancellationToken ct=default)=>db.WorkArtifacts.AsNoTracking().Where(x=>x.JobId==jobId&&x.Key==key).Select(x=>x.Value).SingleOrDefaultAsync(ct);
    public async Task PutAsync(Guid jobId,string key,string value,CancellationToken ct=default)
    {
        var now=DateTimeOffset.UtcNow;
        var existing=await db.WorkArtifacts.SingleOrDefaultAsync(x=>x.JobId==jobId&&x.Key==key,ct);
        if(existing is null)db.WorkArtifacts.Add(new(){Id=Guid.NewGuid(),JobId=jobId,Key=key,Value=value,CreatedAt=now,UpdatedAt=now});
        else{existing.Value=value;existing.UpdatedAt=now;}
        await db.SaveChangesAsync(ct);
    }
}