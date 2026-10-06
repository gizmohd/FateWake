using Microsoft.EntityFrameworkCore;
namespace Fatewake.Infrastructure.Persistence;
/// <summary>Implements PostgreSQL-backed job artifact exchange with unique job/key identity.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/WorkArtifactStore.md">WorkArtifactStore documentation</see>. Writes use a database upsert so concurrent retries converge safely.</remarks>
public sealed class WorkArtifactStore(FatewakeDbContext db):IWorkArtifactStore
{
    public Task<string?> GetAsync(Guid jobId,string key,CancellationToken ct=default)=>db.WorkArtifacts.AsNoTracking().Where(x=>x.JobId==jobId&&x.Key==key).Select(x=>x.Value).SingleOrDefaultAsync(ct);

    public async Task PutAsync(Guid jobId,string key,string value,CancellationToken ct=default)
    {
        var id=Guid.NewGuid();
        var now=DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO work_artifact (""Id"",""JobId"",""Key"",""Value"",""CreatedAt"",""UpdatedAt"")
VALUES ({id},{jobId},{key},{value}::jsonb,{now},{now})
ON CONFLICT (""JobId"",""Key"") DO UPDATE SET ""Value""=EXCLUDED.""Value"",""UpdatedAt""=EXCLUDED.""UpdatedAt"";",ct);
    }
}