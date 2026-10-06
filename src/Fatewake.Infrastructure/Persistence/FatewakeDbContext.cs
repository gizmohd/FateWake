using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

public sealed class FatewakeDbContext(DbContextOptions<FatewakeDbContext> options) : DbContext(options)
{
    public DbSet<AccountRecord> Accounts => Set<AccountRecord>(); public DbSet<ExternalIdentityRecord> ExternalIdentities => Set<ExternalIdentityRecord>(); public DbSet<RealmRecord> Realms => Set<RealmRecord>(); public DbSet<TimelineRecord> Timelines => Set<TimelineRecord>(); public DbSet<SurvivorRecord> Survivors => Set<SurvivorRecord>(); public DbSet<EventInstanceRecord> EventInstances => Set<EventInstanceRecord>(); public DbSet<ActionAttemptRecord> ActionAttempts => Set<ActionAttemptRecord>(); public DbSet<ActionResolutionRecord> ActionResolutions => Set<ActionResolutionRecord>(); public DbSet<GameEventRecord> GameEvents => Set<GameEventRecord>(); public DbSet<WakeRecord> Wakes => Set<WakeRecord>();
    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<AccountRecord>(b=>{b.ToTable("account");b.HasKey(x=>x.Id);});\n        m.Entity<ExternalIdentityRecord>(b=>{b.ToTable("external_identity");b.HasKey(x=>x.Id);b.HasIndex(x=>new{x.Provider,x.ProviderSubject}).IsUnique();b.HasIndex(x=>x.AccountId);b.Property(x=>x.ClaimsSnapshot).HasColumnType("jsonb");});\n        m.Entity<RealmRecord>(b=>{b.ToTable("realm");b.HasKey(x=>x.Id);b.HasIndex(x=>x.Key).IsUnique();b.Property(x=>x.Properties).HasColumnType("jsonb");});
        m.Entity<TimelineRecord>(b=>{b.ToTable("timeline");b.HasKey(x=>x.Id);});
        m.Entity<SurvivorRecord>(b=>{b.ToTable("survivor");b.HasKey(x=>x.Id);b.HasIndex(x=>x.TimelineId);});
        m.Entity<EventInstanceRecord>(b=>{b.ToTable("event_instance");b.HasKey(x=>x.Id);b.Property(x=>x.State).HasColumnType("jsonb");b.Property(x=>x.Version).IsConcurrencyToken();});
        m.Entity<ActionAttemptRecord>(b=>{b.ToTable("action_attempt");b.HasKey(x=>x.Id);b.Property(x=>x.CandidateAction).HasColumnType("jsonb");b.HasIndex(x=>new{x.EventInstanceId,x.IdempotencyKey}).IsUnique();});
        m.Entity<ActionResolutionRecord>(b=>{b.ToTable("action_resolution");b.HasKey(x=>x.Id);b.HasIndex(x=>x.ActionAttemptId).IsUnique();b.Property(x=>x.ResolvedAction).HasColumnType("jsonb");b.Property(x=>x.AuthoritativeEffects).HasColumnType("jsonb");b.Property(x=>x.NarrativeFacts).HasColumnType("jsonb");});
        m.Entity<GameEventRecord>(b=>{b.ToTable("game_event");b.HasKey(x=>x.Id);b.Property(x=>x.Payload).HasColumnType("jsonb");b.HasIndex(x=>new{x.TimelineId,x.Sequence}).IsUnique();});
        m.Entity<WakeRecord>(b=>{b.ToTable("wake");b.HasKey(x=>x.Id);b.Property(x=>x.Properties).HasColumnType("jsonb");b.HasIndex(x=>x.OriginEventId);});
    }
}

public sealed class GameEventRecord { public Guid Id{get;set;} public Guid TimelineId{get;set;} public Guid? SurvivorId{get;set;} public required string EventType{get;set;} public long Sequence{get;set;} public int SurvivorDay{get;set;} public Guid? CausationEventId{get;set;} public Guid CorrelationId{get;set;} public required string Payload{get;set;} public int SchemaVersion{get;set;}=1; public DateTimeOffset OccurredAt{get;set;} }
public sealed class WakeRecord { public Guid Id{get;set;} public Guid TimelineId{get;set;} public Guid OriginEventId{get;set;} public required string WakeType{get;set;} public required string Scope{get;set;} public string? TargetEntityType{get;set;} public Guid? TargetEntityId{get;set;} public short Severity{get;set;} public required string State{get;set;} public required string Properties{get;set;} public int CreatedDay{get;set;} public DateTimeOffset CreatedAt{get;set;} }
