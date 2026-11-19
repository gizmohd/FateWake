using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Configures the generated scene table with one scene per survivor timeline sequence.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/GeneratedSceneModel.md">Documentation</see>
public static class GeneratedSceneModel
{
    /// <summary>Maps scenes to restrictive survivor and timeline keys with a unique sequence for idempotent reservation.</summary>
    public static void Configure(ModelBuilder model) => model.Entity<GeneratedSceneRecord>(entity =>
    {
        entity.ToTable("generated_scene");
        entity.HasKey(row => row.Id);
        entity.HasIndex(row => new { row.SurvivorId, row.TimelineId, row.Sequence }).IsUnique();
        entity.HasIndex(row => new { row.BranchKey, row.ContentLevel })
            .HasDatabaseName("ix_generated_scene_branch").HasFilter("\"BranchKey\" IS NOT NULL");
        entity.Property(row => row.Status).HasMaxLength(20);
        entity.Property(row => row.Title).HasMaxLength(200);
        entity.Property(row => row.ChosenActionType).HasMaxLength(40);
        entity.Property(row => row.ChosenText).HasMaxLength(500);
        entity.Property(row => row.Spec).HasColumnType("jsonb");
        entity.HasOne<SurvivorRecord>().WithMany().HasForeignKey(row => row.SurvivorId).OnDelete(DeleteBehavior.NoAction);
        entity.HasOne<TimelineRecord>().WithMany().HasForeignKey(row => row.TimelineId).OnDelete(DeleteBehavior.NoAction);
    });
}
