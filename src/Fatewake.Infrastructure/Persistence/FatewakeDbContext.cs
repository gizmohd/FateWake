using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

public sealed class FatewakeDbContext(DbContextOptions<FatewakeDbContext> options) : DbContext(options)
{
    public DbSet<GameEventRecord> GameEvents => Set<GameEventRecord>();
    public DbSet<WakeRecord> Wakes => Set<WakeRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameEventRecord>(b => { b.ToTable("game_event"); b.HasKey(x => x.Id); b.Property(x => x.Payload).HasColumnType("jsonb"); b.HasIndex(x => new { x.TimelineId, x.Sequence }).IsUnique(); });
        modelBuilder.Entity<WakeRecord>(b => { b.ToTable("wake"); b.HasKey(x => x.Id); b.Property(x => x.Properties).HasColumnType("jsonb"); b.HasIndex(x => x.OriginEventId); });
    }
}

public sealed class GameEventRecord
{
    public Guid Id { get; set; }
    public Guid TimelineId { get; set; }
    public Guid? SurvivorId { get; set; }
    public required string EventType { get; set; }
    public long Sequence { get; set; }
    public int SurvivorDay { get; set; }
    public Guid? CausationEventId { get; set; }
    public Guid CorrelationId { get; set; }
    public required string Payload { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class WakeRecord
{
    public Guid Id { get; set; }
    public Guid TimelineId { get; set; }
    public Guid OriginEventId { get; set; }
    public required string WakeType { get; set; }
    public required string Scope { get; set; }
    public string? TargetEntityType { get; set; }
    public Guid? TargetEntityId { get; set; }
    public short Severity { get; set; }
    public required string State { get; set; }
    public required string Properties { get; set; }
    public int CreatedDay { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
