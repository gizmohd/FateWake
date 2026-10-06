using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

public sealed class FatewakeDbContext(DbContextOptions<FatewakeDbContext> options) : DbContext(options)
{
    public DbSet<WorkJobRecord> WorkJobs => Set<WorkJobRecord>(); public DbSet<WorkArtifactRecord> WorkArtifacts => Set<WorkArtifactRecord>(); public DbSet<WorkStepRecord> WorkSteps => Set<WorkStepRecord>(); public DbSet<WorkStepDependencyRecord> WorkStepDependencies => Set<WorkStepDependencyRecord>(); public DbSet<AccountRecord> Accounts => Set<AccountRecord>(); public DbSet<ExternalIdentityRecord> ExternalIdentities => Set<ExternalIdentityRecord>(); public DbSet<ArtAssetRecord> ArtAssets => Set<ArtAssetRecord>(); public DbSet<ArtAssetDerivativeRecord> ArtAssetDerivatives => Set<ArtAssetDerivativeRecord>(); public DbSet<ArtGenerationRecord> ArtGenerations => Set<ArtGenerationRecord>(); public DbSet<RealmRecord> Realms => Set<RealmRecord>(); public DbSet<TimelineRecord> Timelines => Set<TimelineRecord>(); public DbSet<SurvivorRecord> Survivors => Set<SurvivorRecord>(); public DbSet<EventInstanceRecord> EventInstances => Set<EventInstanceRecord>(); public DbSet<ActionAttemptRecord> ActionAttempts => Set<ActionAttemptRecord>(); public DbSet<ActionResolutionRecord> ActionResolutions => Set<ActionResolutionRecord>(); public DbSet<GameEventRecord> GameEvents => Set<GameEventRecord>(); public DbSet<WakeRecord> Wakes => Set<WakeRecord>();
    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<WorkArtifactRecord>(b=>{b.ToTable("work_artifact");b.HasKey(x=>x.Id);b.HasIndex(x=>new{x.JobId,x.Key}).IsUnique();b.Property(x=>x.Value).HasColumnType("jsonb");b.HasOne<WorkJobRecord>().WithMany().HasForeignKey(x=>x.JobId).OnDelete(DeleteBehavior.Cascade);});
        m.Entity<WorkJobRecord>(b=>{b.ToTable("work_job");b.HasKey(x=>x.Id);b.HasIndex(x=>new{x.JobType,x.IdempotencyKey}).IsUnique();b.Property(x=>x.Status).HasConversion<int>();b.Property(x=>x.Payload).HasColumnType("jsonb");});
        m.Entity<WorkStepRecord>(b=>{b.ToTable("work_step");b.HasKey(x=>x.Id);b.HasIndex(x=>new{x.Queue,x.Status,x.NextEligibleAt,x.Priority});b.HasIndex(x=>x.LeaseExpiresAt);b.Property(x=>x.Status).HasConversion<int>();b.Property(x=>x.Input).HasColumnType("jsonb");b.Property(x=>x.Output).HasColumnType("jsonb");b.HasOne<WorkJobRecord>().WithMany().HasForeignKey(x=>x.JobId).OnDelete(DeleteBehavior.Cascade);});
        m.Entity<WorkStepDependencyRecord>(b=>{b.ToTable("work_step_dependency");b.HasKey(x=>new{x.StepId,x.DependsOnStepId});b.HasOne<WorkStepRecord>().WithMany().HasForeignKey(x=>x.StepId).OnDelete(DeleteBehavior.Cascade);b.HasOne<WorkStepRecord>().WithMany().HasForeignKey(x=>x.DependsOnStepId).OnDelete(DeleteBehavior.NoAction);});
        m.Entity<AccountRecord>(b=>{b.ToTable("account");b.HasKey(x=>x.Id);});
        m.Entity<ArtAssetRecord>(b=>{b.ToTable("art_asset");b.HasKey(x=>x.Id);b.HasIndex(x=>new{x.Key,x.Version}).IsUnique();b.HasIndex(x=>x.VisualFingerprint);b.HasIndex(x=>x.ContentHash);b.Property(x=>x.Status).HasConversion<int>();b.HasOne<ArtAssetRecord>().WithMany().HasForeignKey(x=>x.ParentAssetId).OnDelete(DeleteBehavior.NoAction);});
        m.Entity<ArtAssetDerivativeRecord>(b=>{b.ToTable("art_asset_derivative");b.HasKey(x=>x.Id);b.HasIndex(x=>new{x.ArtAssetId,x.Kind}).IsUnique();b.Property(x=>x.Kind).HasConversion<int>();b.HasOne<ArtAssetRecord>().WithMany().HasForeignKey(x=>x.ArtAssetId).OnDelete(DeleteBehavior.Cascade);});
        m.Entity<ArtGenerationRecord>(b=>{b.ToTable("art_generation");b.HasKey(x=>x.Id);b.HasIndex(x=>x.ArtAssetId);b.Property(x=>x.ReferenceAssets).HasColumnType("jsonb");b.Property(x=>x.GenerationParameters).HasColumnType("jsonb");b.Property(x=>x.ContinuityVersions).HasColumnType("jsonb");b.HasOne<ArtAssetRecord>().WithMany().HasForeignKey(x=>x.ArtAssetId).OnDelete(DeleteBehavior.NoAction);});
        m.Entity<ExternalIdentityRecord>(b=>{b.ToTable("external_identity");b.HasKey(x=>x.Id);b.HasIndex(x=>new{x.Provider,x.ProviderSubject}).IsUnique();b.HasIndex(x=>x.AccountId);b.Property(x=>x.ClaimsSnapshot).HasColumnType("jsonb");});
        m.Entity<RealmRecord>(b=>{b.ToTable("realm");b.HasKey(x=>x.Id);b.HasIndex(x=>x.Key).IsUnique();b.Property(x=>x.Properties).HasColumnType("jsonb");});
        m.Entity<TimelineRecord>(b=>{b.ToTable("timeline");b.HasKey(x=>x.Id);});
        m.Entity<SurvivorRecord>(b=>{b.ToTable("survivor");b.HasKey(x=>x.Id);b.HasIndex(x=>x.TimelineId);});
        m.Entity<EventInstanceRecord>(b=>{b.ToTable("event_instance");b.HasKey(x=>x.Id);b.Property(x=>x.State).HasColumnType("jsonb");b.Property(x=>x.Version).IsConcurrencyToken();});
        m.Entity<ActionAttemptRecord>(b=>{b.ToTable("action_attempt");b.HasKey(x=>x.Id);b.Property(x=>x.CandidateAction).HasColumnType("jsonb");b.HasIndex(x=>new{x.EventInstanceId,x.IdempotencyKey}).IsUnique();});
        m.Entity<ActionResolutionRecord>(b=>{b.ToTable("action_resolution");b.HasKey(x=>x.Id);b.HasIndex(x=>x.ActionAttemptId).IsUnique();b.Property(x=>x.ResolvedAction).HasColumnType("jsonb");b.Property(x=>x.AuthoritativeEffects).HasColumnType("jsonb");b.Property(x=>x.NarrativeFacts).HasColumnType("jsonb");});
        m.Entity<GameEventRecord>(b=>{b.ToTable("game_event");b.HasKey(x=>x.Id);b.Property(x=>x.Payload).HasColumnType("jsonb");b.HasIndex(x=>new{x.TimelineId,x.Sequence}).IsUnique();});
        m.Entity<WakeRecord>(b=>{b.ToTable("wake");b.HasKey(x=>x.Id);b.Property(x=>x.Properties).HasColumnType("jsonb");b.HasIndex(x=>x.OriginEventId);});
        m.Entity<AccountRecord>().Property(x=>x.Status).HasConversion<int>();
        m.Entity<SurvivorRecord>().Property(x=>x.Status).HasConversion<int>();
        m.Entity<EventInstanceRecord>().Property(x=>x.Status).HasConversion<int>();
        m.Entity<WakeRecord>().Property(x=>x.State).HasConversion<int>();
        m.Entity<EventInstanceRecord>().HasIndex(x=>new{x.SurvivorId,x.Status,x.SurvivorDay});
        m.Entity<ExternalIdentityRecord>().HasOne<AccountRecord>().WithMany().HasForeignKey(x=>x.AccountId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<TimelineRecord>().HasOne<RealmRecord>().WithMany().HasForeignKey(x=>x.RealmId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<SurvivorRecord>().HasOne<TimelineRecord>().WithMany().HasForeignKey(x=>x.TimelineId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<EventInstanceRecord>().HasOne<TimelineRecord>().WithMany().HasForeignKey(x=>x.TimelineId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<EventInstanceRecord>().HasOne<SurvivorRecord>().WithMany().HasForeignKey(x=>x.SurvivorId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<ActionAttemptRecord>().HasOne<EventInstanceRecord>().WithMany().HasForeignKey(x=>x.EventInstanceId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<ActionAttemptRecord>().HasOne<SurvivorRecord>().WithMany().HasForeignKey(x=>x.SurvivorId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<ActionResolutionRecord>().HasOne<ActionAttemptRecord>().WithOne().HasForeignKey<ActionResolutionRecord>(x=>x.ActionAttemptId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<GameEventRecord>().HasOne<TimelineRecord>().WithMany().HasForeignKey(x=>x.TimelineId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<GameEventRecord>().HasOne<SurvivorRecord>().WithMany().HasForeignKey(x=>x.SurvivorId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<GameEventRecord>().HasOne<GameEventRecord>().WithMany().HasForeignKey(x=>x.CausationEventId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<WakeRecord>().HasOne<TimelineRecord>().WithMany().HasForeignKey(x=>x.TimelineId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<WakeRecord>().HasOne<GameEventRecord>().WithMany().HasForeignKey(x=>x.OriginEventId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class GameEventRecord { public Guid Id{get;set;} public Guid TimelineId{get;set;} public Guid? SurvivorId{get;set;} public required string EventType{get;set;} public long Sequence{get;set;} public int SurvivorDay{get;set;} public Guid? CausationEventId{get;set;} public Guid CorrelationId{get;set;} public required string Payload{get;set;} public int SchemaVersion{get;set;}=1; public DateTimeOffset OccurredAt{get;set;} }
public sealed class WakeRecord { public Guid Id{get;set;} public Guid TimelineId{get;set;} public Guid OriginEventId{get;set;} public required string WakeType{get;set;} public required string Scope{get;set;} public string? TargetEntityType{get;set;} public Guid? TargetEntityId{get;set;} public short Severity{get;set;} public required WakeState State{get;set;} public required string Properties{get;set;} public int CreatedDay{get;set;} public DateTimeOffset CreatedAt{get;set;} }
