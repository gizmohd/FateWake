using Fatewake.Infrastructure.Work;
namespace Fatewake.Infrastructure.Art;
/// <summary>Creates the durable fan-out/fan-in work graph for a canonical artwork request.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/IArtWorkJobFactory.md">IArtWorkJobFactory documentation</see>.</remarks>
public interface IArtWorkJobFactory
{
    Task<CreatedWorkJob> CreateAsync(ArtWorkRequest request, CancellationToken ct = default);
    /// <summary>Creates image work and commits related prompt revision state in the same PostgreSQL transaction.</summary>
    Task<CreatedWorkJob> CreateAsync(ArtWorkRequest request, Func<Guid, CancellationToken, Task> transactionWork, CancellationToken ct = default);
}