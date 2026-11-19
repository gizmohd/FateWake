using Fatewake.Infrastructure.Authentication;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Maps authoritative gameplay, account, work, corpus, and shared Data Protection key-ring state.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/FatewakeDbContext.md">Documentation</see>
public sealed class FatewakeDbContext(DbContextOptions<FatewakeDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    /// <summary>Persists the shared key ring; application discriminators isolate each host's protected payloads.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
    /// <summary>Contains password-encrypted private certificates, never a plaintext certificate password.</summary>
    public DbSet<DataProtectionCertificateRecord> DataProtectionCertificates => Set<DataProtectionCertificateRecord>();
    /// <summary>Server-translated calendar grouping in the selected reporting timezone.</summary>
    public static string CalendarKey(DateTimeOffset value, string zone, string pattern) => throw new NotSupportedException("SQL only.");
    /// <summary>Server-translated extraction of safe job metadata from JSON.</summary>
    public static string? JsonText(string value, string field) => throw new NotSupportedException("SQL only.");
    public DbSet<AccountEmailRecord> AccountEmails => Set<AccountEmailRecord>();
    public DbSet<EmailVerificationRecord> EmailVerifications => Set<EmailVerificationRecord>();
    /// <summary>Private registration details; legacy accounts need no backfilled or fabricated profile.</summary>
    public DbSet<AccountProfileRecord> AccountProfiles => Set<AccountProfileRecord>();
    public DbSet<LocalCredentialRecord> LocalCredentials => Set<LocalCredentialRecord>();
    public DbSet<ArtBinaryRecord> ArtBinaries => Set<ArtBinaryRecord>();
    public DbSet<SurvivorVisualIdentityRecord> SurvivorVisualIdentities => Set<SurvivorVisualIdentityRecord>(); public DbSet<WorkJobRecord> WorkJobs => Set<WorkJobRecord>(); public DbSet<WorkArtifactRecord> WorkArtifacts => Set<WorkArtifactRecord>(); public DbSet<WorkStepRecord> WorkSteps => Set<WorkStepRecord>(); public DbSet<WorkStepDependencyRecord> WorkStepDependencies => Set<WorkStepDependencyRecord>(); public DbSet<AccountRecord> Accounts => Set<AccountRecord>(); public DbSet<ExternalIdentityRecord> ExternalIdentities => Set<ExternalIdentityRecord>(); public DbSet<ArtAssetRecord> ArtAssets => Set<ArtAssetRecord>(); public DbSet<ArtAssetDerivativeRecord> ArtAssetDerivatives => Set<ArtAssetDerivativeRecord>(); public DbSet<ArtGenerationRecord> ArtGenerations => Set<ArtGenerationRecord>(); public DbSet<ArtRegenerationRecord> ArtRegenerationRequests => Set<ArtRegenerationRecord>(); public DbSet<RagSourceRecord> RagSources => Set<RagSourceRecord>(); public DbSet<RagDocumentRecord> RagDocuments => Set<RagDocumentRecord>(); public DbSet<RagChunkRecord> RagChunks => Set<RagChunkRecord>(); public DbSet<RagClaimRecord> RagClaims => Set<RagClaimRecord>(); public DbSet<RagIngestRunRecord> RagIngestRuns => Set<RagIngestRunRecord>(); public DbSet<RagStagingRecord> RagStaging => Set<RagStagingRecord>(); public DbSet<RealmRecord> Realms => Set<RealmRecord>(); public DbSet<TimelineRecord> Timelines => Set<TimelineRecord>(); public DbSet<SurvivorRecord> Survivors => Set<SurvivorRecord>(); public DbSet<EventInstanceRecord> EventInstances => Set<EventInstanceRecord>(); public DbSet<ActionAttemptRecord> ActionAttempts => Set<ActionAttemptRecord>(); public DbSet<ActionResolutionRecord> ActionResolutions => Set<ActionResolutionRecord>(); public DbSet<GameEventRecord> GameEvents => Set<GameEventRecord>(); public DbSet<WakeRecord> Wakes => Set<WakeRecord>();
    protected override void OnModelCreating(ModelBuilder m)
    {
        TimelineModel.Configure(m);
        GeneratedSceneModel.Configure(m);
        ArtPromptModel.Configure(m);
        m.Entity<DataProtectionKey>().ToTable("data_protection_keys");
        m.Entity<DataProtectionCertificateRecord>(entity =>
        {
            entity.ToTable("data_protection_certificates");
            entity.HasKey(row => row.Thumbprint);
            entity.HasIndex(row => row.NotAfter);
            entity.Property(row => row.Thumbprint).HasMaxLength(64);
            entity.Property(row => row.EncryptedPfx).HasColumnType("bytea");
        });
        m.UseOpenIddict();
        m.Entity<OpenIddictEntityFrameworkCoreApplication>().ToTable("OpenIddictApplications", "oidc");
        m.Entity<OpenIddictEntityFrameworkCoreAuthorization>().ToTable("OpenIddictAuthorizations", "oidc");
        m.Entity<OpenIddictEntityFrameworkCoreScope>().ToTable("OpenIddictScopes", "oidc");
        m.Entity<OpenIddictEntityFrameworkCoreToken>().ToTable("OpenIddictTokens", "oidc");
        m.HasDbFunction(typeof(FatewakeDbContext).GetMethod(nameof(CalendarKey))!).HasName("ai_calendar_key");
        m.HasDbFunction(typeof(FatewakeDbContext).GetMethod(nameof(JsonText))!).HasName("ai_json_text");
        m.Entity<AiUsageRecord>(b =>
        {
            b.ToTable("ai_usage");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.Provider, x.AttemptKey }).IsUnique();
            b.HasIndex(x => new { x.Provider, x.AccountingKey }).IsUnique().HasFilter("\"AccountingKey\" IS NOT NULL");
            b.HasIndex(x => x.StartedAt);
            b.HasIndex(x => new { x.Provider, x.Model, x.Operation, x.StartedAt });
            b.HasIndex(x => x.WorkJobId);
        });
        m.Entity<AiUsageTrackingRecord>(b => { b.ToTable("ai_usage_tracking"); b.HasKey(x => x.Id); });
        m.Entity<StoryPanelRecord>(b =>
        {
            b.ToTable("story_panel");
            b.HasKey(x => x.Id);
            b.Property(x => x.EntryKey).HasMaxLength(200);
            b.Property(x => x.Kind).HasMaxLength(30);
            b.Property(x => x.Layers).HasColumnType("jsonb");
            b.HasIndex(x => new { x.SurvivorId, x.TimelineId, x.EntryKey }).IsUnique();
            b.HasIndex(x => new { x.TimelineId, x.Sequence }).IsUnique();
            b.HasOne<SurvivorRecord>().WithMany().HasForeignKey(x => x.SurvivorId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<TimelineRecord>().WithMany().HasForeignKey(x => x.TimelineId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<EventInstanceRecord>().WithMany().HasForeignKey(x => x.EventInstanceId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<GameEventRecord>().WithMany().HasForeignKey(x => x.SourceEventId).OnDelete(DeleteBehavior.NoAction);
        });
        m.Entity<AccountRoleRecord>(b =>
        {
            b.ToTable("account_role");
            b.HasKey(x => x.AccountId);
            b.Property(x => x.Role).HasMaxLength(10);
            b.HasOne<AccountRecord>().WithOne().HasForeignKey<AccountRoleRecord>(x => x.AccountId).OnDelete(DeleteBehavior.NoAction);
        });
        m.Entity<ArtReviewRecord>(b =>
        {
            b.ToTable("art_review");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.SceneKey, x.CreatedAt });
            b.HasIndex(x => new { x.AssetKey, x.CreatedAt });
            b.Property(x => x.CompositionVersions).HasColumnType("jsonb");
            b.Property(x => x.SceneKey).HasMaxLength(100);
            b.Property(x => x.AssetKey).HasMaxLength(100);
            b.Property(x => x.Critique).HasMaxLength(4000);
            b.Property(x => x.Correction).HasMaxLength(4000);
            b.HasOne<AccountRecord>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.NoAction);
        });
        m.Entity<AccountEmailRecord>(b =>
        {
            b.ToTable("account_email");
            b.HasKey(x => x.AccountId);
            b.HasIndex(x => x.NormalizedEmail).IsUnique();
            b.Property(x => x.NormalizedEmail).HasMaxLength(254);
            b.HasOne<AccountRecord>().WithOne().HasForeignKey<AccountEmailRecord>(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        m.Entity<EmailVerificationRecord>(b =>
        {
            b.ToTable("email_verification");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.TokenHash).IsUnique();
            b.HasIndex(x => x.NormalizedEmail);
            b.Property(x => x.FirstName).HasMaxLength(AccountProfileDetails.MaximumLength);
            b.Property(x => x.LastName).HasMaxLength(AccountProfileDetails.MaximumLength);
            b.Property(x => x.City).HasMaxLength(AccountProfileDetails.MaximumLength);
            b.Property(x => x.State).HasMaxLength(AccountProfileDetails.MaximumLength);
        });
        m.Entity<AccountProfileRecord>(b =>
        {
            b.ToTable("account_profile");
            b.HasKey(x => x.AccountId);
            b.HasOne<AccountRecord>().WithOne().HasForeignKey<AccountProfileRecord>(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            b.Property(x => x.FirstName).HasMaxLength(AccountProfileDetails.MaximumLength);
            b.Property(x => x.LastName).HasMaxLength(AccountProfileDetails.MaximumLength);
            b.Property(x => x.City).HasMaxLength(AccountProfileDetails.MaximumLength);
            b.Property(x => x.State).HasMaxLength(AccountProfileDetails.MaximumLength);
            b.Ignore(x => x.IsComplete);
        });
        m.Entity<LocalCredentialRecord>(b =>
        {
            b.ToTable("local_credential");
            b.HasKey(x => x.AccountId);
            b.HasIndex(x => x.NormalizedEmail).IsUnique();
            b.Property(x => x.NormalizedEmail).HasMaxLength(254);
            b.HasOne<AccountRecord>().WithOne().HasForeignKey<LocalCredentialRecord>(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        m.Entity<SurvivorVisualIdentityRecord>(b => { b.ToTable("survivor_visual_identity"); b.HasKey(x => x.SurvivorId); b.HasIndex(x => x.RequestedVisualFingerprint); b.HasIndex(x => x.RequestedWorkJobId); b.Property(x => x.Status).HasConversion<int>(); b.HasOne<SurvivorRecord>().WithOne().HasForeignKey<SurvivorVisualIdentityRecord>(x => x.SurvivorId).OnDelete(DeleteBehavior.Cascade); b.HasOne<ArtAssetRecord>().WithMany().HasForeignKey(x => x.ActiveArtAssetId).OnDelete(DeleteBehavior.NoAction); });
        m.Entity<WorkArtifactRecord>(b => { b.ToTable("work_artifact"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.JobId, x.Key }).IsUnique(); b.Property(x => x.Value).HasColumnType("jsonb"); b.HasOne<WorkJobRecord>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade); });
        m.Entity<WorkJobRecord>(b => { b.ToTable("work_job"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.JobType, x.IdempotencyKey }).IsUnique(); b.Property(x => x.Status).HasConversion<int>(); b.Property(x => x.Payload).HasColumnType("jsonb"); });
        m.Entity<WorkStepRecord>(b => { b.ToTable("work_step"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.Queue, x.Status, x.NextEligibleAt, x.Priority }); b.HasIndex(x => x.LeaseExpiresAt); b.Property(x => x.Status).HasConversion<int>(); b.Property(x => x.Input).HasColumnType("jsonb"); b.Property(x => x.Output).HasColumnType("jsonb"); b.HasOne<WorkJobRecord>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade); });
        m.Entity<WorkStepDependencyRecord>(b => { b.ToTable("work_step_dependency"); b.HasKey(x => new { x.StepId, x.DependsOnStepId }); b.HasOne<WorkStepRecord>().WithMany().HasForeignKey(x => x.StepId).OnDelete(DeleteBehavior.Cascade); b.HasOne<WorkStepRecord>().WithMany().HasForeignKey(x => x.DependsOnStepId).OnDelete(DeleteBehavior.NoAction); });
        m.Entity<AccountRecord>(b => { b.ToTable("account"); b.HasKey(x => x.Id); });
        m.Entity<ArtAssetRecord>(b => { b.ToTable("art_asset"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.Key, x.Version }).IsUnique(); b.HasIndex(x => x.VisualFingerprint); b.HasIndex(x => x.ContentHash); b.HasIndex(x => x.OwnerAccountId); b.Property(x => x.Status).HasConversion<int>(); b.HasOne<ArtAssetRecord>().WithMany().HasForeignKey(x => x.ParentAssetId).OnDelete(DeleteBehavior.NoAction); b.HasOne<AccountRecord>().WithMany().HasForeignKey(x => x.OwnerAccountId).OnDelete(DeleteBehavior.NoAction); });
        m.Entity<ArtBinaryRecord>(b => { b.ToTable("art_binary"); b.HasKey(x => x.StorageKey); b.HasIndex(x => x.ContentHash); b.Property(x => x.Bytes).HasColumnType("bytea"); b.Property(x => x.StorageKey).HasMaxLength(1024); b.Property(x => x.ContentType).HasMaxLength(128); b.Property(x => x.ContentHash).HasMaxLength(64); });
        m.Entity<ArtAssetDerivativeRecord>(b => { b.ToTable("art_asset_derivative"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.ArtAssetId, x.Kind }).IsUnique(); b.Property(x => x.Kind).HasConversion<int>(); b.HasOne<ArtAssetRecord>().WithMany().HasForeignKey(x => x.ArtAssetId).OnDelete(DeleteBehavior.Cascade); });
        m.Entity<ArtGenerationRecord>(b => { b.ToTable("art_generation"); b.HasKey(x => x.Id); b.HasIndex(x => x.WorkJobId).IsUnique(); b.HasIndex(x => x.IdempotencyKey).IsUnique(); b.HasIndex(x => x.ArtAssetId); b.Property(x => x.ReferenceAssets).HasColumnType("jsonb"); b.Property(x => x.GenerationParameters).HasColumnType("jsonb"); b.Property(x => x.ContinuityVersions).HasColumnType("jsonb"); b.HasOne<WorkJobRecord>().WithMany().HasForeignKey(x => x.WorkJobId).OnDelete(DeleteBehavior.Cascade); b.HasOne<ArtAssetRecord>().WithMany().HasForeignKey(x => x.ArtAssetId).OnDelete(DeleteBehavior.NoAction); });
        m.Entity<RagSourceRecord>(b =>
        {
            b.ToTable("rag_source");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.SourceKey).IsUnique();
            b.HasIndex(x => new { x.SourceType, x.ReviewStatus });
            b.Property(x => x.Metadata).HasColumnType("jsonb");
        });
        m.Entity<RagDocumentRecord>(b =>
        {
            b.ToTable("rag_document");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.SourceId, x.DocumentKey }).IsUnique();
            b.HasIndex(x => new { x.DocumentType, x.Status });
            b.Property(x => x.Metadata).HasColumnType("jsonb");
            b.HasOne<RagSourceRecord>().WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Cascade);
        });
        m.Entity<RagChunkRecord>(b =>
        {
            b.ToTable("rag_chunk");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.DocumentId, x.Ordinal }).IsUnique();
            b.HasIndex(x => x.ChunkKey).IsUnique();
            b.HasIndex(x => new { x.TimelineKey, x.SurvivorDayMin, x.SurvivorDayMax });
            b.HasIndex(x => new { x.SpoilerSeriesMin, x.SpoilerSeriesMax });
            b.HasOne<RagDocumentRecord>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });
        m.Entity<RagClaimRecord>(b =>
        {
            b.ToTable("rag_claim");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.Subject, x.Status });
            b.HasIndex(x => x.SourceChunkId);
            b.HasIndex(x => x.SourceDocumentId);
            b.Property(x => x.Metadata).HasColumnType("jsonb");
            b.HasOne<RagChunkRecord>().WithMany().HasForeignKey(x => x.SourceChunkId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<RagDocumentRecord>().WithMany().HasForeignKey(x => x.SourceDocumentId).OnDelete(DeleteBehavior.SetNull);
        });
        m.Entity<RagIngestRunRecord>(b =>
        {
            b.ToTable("rag_ingest_run");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.RunKey).IsUnique();
            b.HasIndex(x => new { x.Status, x.StartedAt });
        });
        m.Entity<RagStagingRecord>(b =>
        {
            b.ToTable("rag_staging");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.ContentHash, x.FileName }).IsUnique();
            b.Property(x => x.Bytes).HasColumnType("bytea");
            b.Property(x => x.FileName).HasMaxLength(1024);
            b.Property(x => x.ContentType).HasMaxLength(128);
        });
        m.Entity<ArtRegenerationRecord>(b =>
        {
            b.ToTable("art_regeneration_request");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.SourceWorkJobId).IsUnique();
            b.HasIndex(x => new { x.Status, x.NextEligibleAt });
            b.HasIndex(x => x.ArtAssetId);
            b.Property(x => x.Status).HasMaxLength(20);
            b.Property(x => x.VisualFingerprint).HasMaxLength(1024);
            b.Property(x => x.TargetProvider).HasMaxLength(50);
            b.Property(x => x.RequestJson).HasColumnType("jsonb");
            b.HasOne<WorkJobRecord>().WithMany().HasForeignKey(x => x.SourceWorkJobId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<ArtAssetRecord>().WithMany().HasForeignKey(x => x.ArtAssetId).OnDelete(DeleteBehavior.NoAction);
        });
        m.Entity<ExternalIdentityRecord>(b => { b.ToTable("external_identity"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.Provider, x.ProviderSubject }).IsUnique(); b.HasIndex(x => x.AccountId); b.Property(x => x.ClaimsSnapshot).HasColumnType("jsonb"); });
        m.Entity<RealmRecord>(b => { b.ToTable("realm"); b.HasKey(x => x.Id); b.HasIndex(x => x.Key).IsUnique(); b.Property(x => x.Properties).HasColumnType("jsonb"); });
        m.Entity<TimelineRecord>(b => { b.ToTable("timeline"); b.HasKey(x => x.Id); });
        m.Entity<SurvivorRecord>(b => { b.ToTable("survivor"); b.HasKey(x => x.Id); b.HasIndex(x => x.TimelineId); b.HasIndex(x => x.AccountId); });
        m.Entity<EventInstanceRecord>(b => { b.ToTable("event_instance"); b.HasKey(x => x.Id); b.Property(x => x.State).HasColumnType("jsonb"); b.Property(x => x.Version).IsConcurrencyToken(); });
        m.Entity<ActionAttemptRecord>(b => { b.ToTable("action_attempt"); b.HasKey(x => x.Id); b.Property(x => x.CandidateAction).HasColumnType("jsonb"); b.HasIndex(x => new { x.EventInstanceId, x.IdempotencyKey }).IsUnique(); });
        m.Entity<ActionResolutionRecord>(b => { b.ToTable("action_resolution"); b.HasKey(x => x.Id); b.HasIndex(x => x.ActionAttemptId).IsUnique(); b.Property(x => x.ResolvedAction).HasColumnType("jsonb"); b.Property(x => x.AuthoritativeEffects).HasColumnType("jsonb"); b.Property(x => x.NarrativeFacts).HasColumnType("jsonb"); });
        m.Entity<GameEventRecord>(b => { b.ToTable("game_event"); b.HasKey(x => x.Id); b.Property(x => x.Payload).HasColumnType("jsonb"); b.HasIndex(x => new { x.TimelineId, x.Sequence }).IsUnique(); });
        m.Entity<WakeRecord>(b => { b.ToTable("wake"); b.HasKey(x => x.Id); b.Property(x => x.Properties).HasColumnType("jsonb"); b.HasIndex(x => x.OriginEventId); });
        m.Entity<AccountRecord>().Property(x => x.Status).HasConversion<int>();
        m.Entity<SurvivorRecord>().Property(x => x.Status).HasConversion<int>();
        m.Entity<EventInstanceRecord>().Property(x => x.Status).HasConversion<int>();
        m.Entity<WakeRecord>().Property(x => x.State).HasConversion<int>();
        m.Entity<EventInstanceRecord>().HasIndex(x => new { x.SurvivorId, x.Status, x.SurvivorDay });
        m.Entity<ExternalIdentityRecord>().HasOne<AccountRecord>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<TimelineRecord>().HasOne<RealmRecord>().WithMany().HasForeignKey(x => x.RealmId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<SurvivorRecord>().HasOne<TimelineRecord>().WithMany().HasForeignKey(x => x.TimelineId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<EventInstanceRecord>().HasOne<TimelineRecord>().WithMany().HasForeignKey(x => x.TimelineId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<EventInstanceRecord>().HasOne<SurvivorRecord>().WithMany().HasForeignKey(x => x.SurvivorId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<ActionAttemptRecord>().HasOne<EventInstanceRecord>().WithMany().HasForeignKey(x => x.EventInstanceId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<ActionAttemptRecord>().HasOne<SurvivorRecord>().WithMany().HasForeignKey(x => x.SurvivorId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<ActionResolutionRecord>().HasOne<ActionAttemptRecord>().WithOne().HasForeignKey<ActionResolutionRecord>(x => x.ActionAttemptId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<GameEventRecord>().HasOne<TimelineRecord>().WithMany().HasForeignKey(x => x.TimelineId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<GameEventRecord>().HasOne<SurvivorRecord>().WithMany().HasForeignKey(x => x.SurvivorId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<GameEventRecord>().HasOne<GameEventRecord>().WithMany().HasForeignKey(x => x.CausationEventId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<WakeRecord>().HasOne<TimelineRecord>().WithMany().HasForeignKey(x => x.TimelineId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<WakeRecord>().HasOne<GameEventRecord>().WithMany().HasForeignKey(x => x.OriginEventId).OnDelete(DeleteBehavior.NoAction);
    }
}
