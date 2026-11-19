using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Configures lineage, checkpoint uniqueness, and isolated branch facts with restrictive history foreign keys.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/TimelineModel.md">Documentation</see>
public static class TimelineModel
{
    /// <summary>Keeps branch identity, immutable checkpoint uniqueness and canonical facts protected by relational constraints.</summary>
    public static void Configure(ModelBuilder model)
    {
        model.Entity<TimelineBranchRecord>(entity =>
        {
            entity.ToTable("timeline_branch");
            entity.HasKey(row => row.TimelineId);
            entity.HasIndex(row => new { row.SurvivorId, row.CreatedAt });
            entity.HasIndex(row => new { row.SurvivorId, row.RequestId }).IsUnique();
            entity.Property(row => row.Origin).HasMaxLength(20);
            entity.HasOne<TimelineRecord>().WithMany().HasForeignKey(row => row.TimelineId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<TimelineRecord>().WithMany().HasForeignKey(row => row.RootTimelineId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<TimelineRecord>().WithMany().HasForeignKey(row => row.ParentTimelineId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<SurvivorRecord>().WithMany().HasForeignKey(row => row.SurvivorId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<TimelineCheckpointRecord>().WithMany().HasForeignKey(row => row.CheckpointId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<TimelineCheckpointRecord>(entity =>
        {
            entity.ToTable("timeline_checkpoint");
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => new { row.TimelineId, row.EventInstanceId, row.Key }).IsUnique();
            entity.HasIndex(row => new { row.SurvivorId, row.CreatedAt });
            entity.Property(row => row.Snapshot).HasColumnType("jsonb");
            entity.Property(row => row.Key).HasMaxLength(100);
            entity.Property(row => row.Label).HasMaxLength(200);
            entity.Property(row => row.TimeLabel).HasMaxLength(100);
            entity.Property(row => row.SnapshotHash).HasMaxLength(64);
            entity.HasOne<TimelineRecord>().WithMany().HasForeignKey(row => row.TimelineId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<SurvivorRecord>().WithMany().HasForeignKey(row => row.SurvivorId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<EventInstanceRecord>().WithMany().HasForeignKey(row => row.EventInstanceId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<SurvivorTimelineStateRecord>(entity =>
        {
            entity.ToTable("survivor_timeline_state");
            entity.HasKey(row => new { row.SurvivorId, row.TimelineId });
            entity.Property(row => row.Facts).HasColumnType("jsonb");
            entity.Property(row => row.Version).IsConcurrencyToken();
            entity.HasOne<SurvivorRecord>().WithMany().HasForeignKey(row => row.SurvivorId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<TimelineRecord>().WithMany().HasForeignKey(row => row.TimelineId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
