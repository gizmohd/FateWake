using BenchmarkDotNet.Attributes;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.Benchmarks;

/// <summary>Measures the PostgreSQL authorization hot-path query for usable and disabled accounts.</summary>
/// <see href="../../docs/code/benchmarks/Fatewake.Benchmarks/AccountAccessBenchmarks.md">Documentation</see>
[MemoryDiagnoser]
public class AccountAccessBenchmarks
{
    private DbContextOptions<FatewakeDbContext> _options = null!;
    private readonly Guid _active = Guid.NewGuid();
    private readonly Guid _disabled = Guid.NewGuid();

    /// <summary>Creates fixtures in an explicitly supplied disposable benchmark database.</summary>
    [GlobalSetup]
    public async Task SetUp()
    {
        _options = new DbContextOptionsBuilder<FatewakeDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("FATEWAKE_AUTH_BENCHMARK_CONNECTION")).Options;
        await using var db = new FatewakeDbContext(_options);
        await DatabaseInitializer.InitializeAsync(db);
        var now = DateTimeOffset.UtcNow;
        db.Accounts.AddRange(
            new AccountRecord { Id = _active, Status = AccountStatus.Active, CreatedAt = now, UpdatedAt = now },
            new AccountRecord { Id = _disabled, Status = AccountStatus.Disabled, CreatedAt = now, UpdatedAt = now });
        db.Set<AccountRoleRecord>().Add(new AccountRoleRecord { AccountId = _active, Role = "Admin", AssignedAt = now });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE account SET "AccessRevokedAt" = {now} WHERE "Id" = {_disabled}""");
    }

    /// <summary>Measures the canonical role and epoch lookup with the stored admin role.</summary>
    [Benchmark(Baseline = true)]
    public async Task<CanonicalAccountAccess?> Active()
    {
        await using var db = new FatewakeDbContext(_options);
        return await CanonicalAccountAccess.FindAsync(db, _active);
    }

    /// <summary>Measures the rejection lookup for a known disabled account.</summary>
    [Benchmark]
    public async Task<CanonicalAccountAccess?> Disabled()
    {
        await using var db = new FatewakeDbContext(_options);
        return await CanonicalAccountAccess.FindAsync(db, _disabled);
    }

    /// <summary>Removes the benchmark's account fixtures, not the database.</summary>
    [GlobalCleanup]
    public async Task CleanUp()
    {
        await using var db = new FatewakeDbContext(_options);
        await db.Set<AccountRoleRecord>().Where(row => row.AccountId == _active).ExecuteDeleteAsync();
        await db.Accounts.Where(row => row.Id == _active || row.Id == _disabled).ExecuteDeleteAsync();
    }
}
