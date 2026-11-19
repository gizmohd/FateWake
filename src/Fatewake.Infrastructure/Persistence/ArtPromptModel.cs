using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Configures prompt persistence without coupling the artwork model to the shared DbContext.</summary>
/// <remarks><see href="../../../../docs/code/src/Fatewake.Infrastructure/Persistence/ArtPromptModel.md">Documentation</see>.</remarks>
public static class ArtPromptModel
{
    /// <summary>Maps prompt identities, immutable revisions, concurrency tokens, and generation provenance.</summary>
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArtPromptRecord>(entity =>
        {
            entity.ToTable("art_prompt");
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => row.IdentityKey).IsUnique();
            entity.HasIndex(row => row.AuthoringJobId);
            entity.HasIndex(row => row.LatestRevisionId);
            entity.HasIndex(row => row.RequestedRevisionId);
            entity.HasIndex(row => row.RequestedAssetId);
            entity.HasIndex(row => row.RequestedWorkJobId);
            entity.HasIndex(row => row.OwnerAccountId);
            entity.Property(row => row.IdentityKey).HasMaxLength(300);
            entity.Property(row => row.AssetKey).HasMaxLength(100);
            entity.Property(row => row.ContextFingerprint).HasMaxLength(64);
            entity.Property(row => row.ReachedVisualContext).HasMaxLength(300);
            entity.Property(row => row.StyleBibleVersion).HasMaxLength(20);
            entity.Property(row => row.Status).HasMaxLength(20);
            entity.Property(row => row.FailureCode).HasMaxLength(40);
            entity.Property(row => row.UpdatedAt).IsConcurrencyToken();
            entity.HasOne<ArtPromptRevisionRecord>().WithMany().HasForeignKey(row => row.CurrentRevisionId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<ArtPromptRevisionRecord>().WithMany().HasForeignKey(row => row.LatestRevisionId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<ArtPromptRevisionRecord>().WithMany().HasForeignKey(row => row.RequestedRevisionId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<ArtAssetRecord>().WithMany().HasForeignKey(row => row.RequestedAssetId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<WorkJobRecord>().WithMany().HasForeignKey(row => row.RequestedWorkJobId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<WorkJobRecord>().WithMany().HasForeignKey(row => row.AuthoringJobId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<AccountRecord>().WithMany().HasForeignKey(row => row.OwnerAccountId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<ArtPromptRevisionRecord>(entity =>
        {
            entity.ToTable("art_prompt_revision");
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => new { row.ArtPromptId, row.RevisionNumber }).IsUnique();
            entity.Property(row => row.PositivePrompt).HasMaxLength(8000);
            entity.Property(row => row.NegativePrompt).HasMaxLength(2000);
            entity.Property(row => row.Source).HasMaxLength(20);
            entity.Property(row => row.Model).HasMaxLength(100);
            entity.Property(row => row.RequestContextFingerprint).HasMaxLength(64);
            entity.HasOne<ArtPromptRecord>().WithMany().HasForeignKey(row => row.ArtPromptId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ArtPromptRevisionRecord>().WithMany().HasForeignKey(row => row.ParentRevisionId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<AccountRecord>().WithMany().HasForeignKey(row => row.CreatorAccountId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<ArtGenerationRecord>()
            .HasOne<ArtPromptRevisionRecord>().WithMany()
            .HasForeignKey(row => row.PromptRevisionId)
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<ArtReviewRecord>(entity =>
        {
            entity.HasIndex(row => row.PromptRevisionId);
            entity.HasIndex(row => row.SourceArtAssetId);
            entity.HasIndex(row => row.RegenerationWorkJobId);
            entity.HasOne<ArtPromptRevisionRecord>().WithMany().HasForeignKey(row => row.PromptRevisionId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<ArtAssetRecord>().WithMany().HasForeignKey(row => row.SourceArtAssetId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<WorkJobRecord>().WithMany().HasForeignKey(row => row.RegenerationWorkJobId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
