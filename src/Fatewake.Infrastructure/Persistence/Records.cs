namespace Fatewake.Infrastructure.Persistence;

public sealed class RealmRecord { public Guid Id { get; set; } public required string Key { get; set; } public required string Name { get; set; } public string Properties { get; set; } = "{}"; }
public sealed class TimelineRecord { public Guid Id { get; set; } public Guid RealmId { get; set; } public required string ProgressionState { get; set; } public required string ConvergenceState { get; set; } public required string WorldClockPolicy { get; set; } public int CurrentSurvivorDay { get; set; } = 1; public long NextEventSequence { get; set; } = 1; public DateTimeOffset CreatedAt { get; set; } }
public sealed class SurvivorRecord { public Guid Id { get; set; } public Guid TimelineId { get; set; } public Guid? AccountId { get; set; } public required string DisplayName { get; set; } public required string IdentityMode { get; set; } public required string BroadRegion { get; set; } public int SurvivorDay { get; set; } = 1; public required SurvivorStatus Status { get; set; } public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset UpdatedAt { get; set; } }
public sealed class EventInstanceRecord { public Guid Id { get; set; } public Guid TimelineId { get; set; } public Guid SurvivorId { get; set; } public required string EventKey { get; set; } public required EventInstanceStatus Status { get; set; } public int SurvivorDay { get; set; } public long Version { get; set; } public string State { get; set; } = "{}"; public DateTimeOffset StartedAt { get; set; } public DateTimeOffset? ResolvedAt { get; set; } }
public sealed class ActionAttemptRecord { public Guid Id { get; set; } public Guid IdempotencyKey { get; set; } public Guid EventInstanceId { get; set; } public Guid SurvivorId { get; set; } public required string InputKind { get; set; } public string? RawInput { get; set; } public string? AuthoredActionKey { get; set; } public required string CandidateAction { get; set; } public DateTimeOffset SubmittedAt { get; set; } }
public sealed class ActionResolutionRecord { public Guid Id { get; set; } public Guid ActionAttemptId { get; set; } public required string OutcomeType { get; set; } public required string ResolvedAction { get; set; } public required string AuthoritativeEffects { get; set; } public required string NarrativeFacts { get; set; } public required string RulesVersion { get; set; } public DateTimeOffset ResolvedAt { get; set; } }

public sealed class AccountRecord { public Guid Id { get; set; } public string? DisplayName { get; set; } public string? PrimaryEmail { get; set; } public required AccountStatus Status { get; set; } public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset UpdatedAt { get; set; } }
public sealed class ExternalIdentityRecord { public Guid Id { get; set; } public Guid AccountId { get; set; } public required string Provider { get; set; } public required string ProviderSubject { get; set; } public string? Email { get; set; } public bool? EmailVerified { get; set; } public string? DisplayName { get; set; } public string? ClaimsSnapshot { get; set; } public DateTimeOffset LinkedAt { get; set; } public DateTimeOffset LastLoginAt { get; set; } }


public enum ArtAssetStatus { Draft=0, Approved=1, Superseded=2, Rejected=3 }
public enum ArtDerivativeKind { MasterPng=0, WebP=1, OptimizedPng=2, Responsive=3 }

public sealed class ArtAssetRecord
{
    public Guid Id { get; set; }
    public required string Key { get; set; }
    public int Version { get; set; } = 1;
    public required string AssetType { get; set; }
    public ArtAssetStatus Status { get; set; } = ArtAssetStatus.Draft;
    public Guid? ParentAssetId { get; set; }
    public string? CharacterKey { get; set; }
    public string? LocationKey { get; set; }
    public required string VisualFingerprint { get; set; }
    public required string ContentHash { get; set; }
    public required string MasterPngStorageKey { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool HasAlpha { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

public sealed class ArtAssetDerivativeRecord
{
    public Guid Id { get; set; }
    public Guid ArtAssetId { get; set; }
    public ArtDerivativeKind Kind { get; set; }
    public required string StorageKey { get; set; }
    public required string ContentType { get; set; }
    public required string ContentHash { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long ByteSize { get; set; }
    public string? EncoderMetadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ArtGenerationRecord
{
    public Guid Id { get; set; }
    public Guid ArtAssetId { get; set; }
    public required string Operation { get; set; }
    public string? PromptKey { get; set; }
    public string? PromptTemplateVersion { get; set; }
    public required string ResolvedPrompt { get; set; }
    public string? NegativePrompt { get; set; }
    public required string Provider { get; set; }
    public required string Model { get; set; }
    public string? ProviderJobId { get; set; }
    public string? PromptBuilderVersion { get; set; }
    public string? StyleBibleVersion { get; set; }
    public string ReferenceAssets { get; set; } = "[]";
    public string GenerationParameters { get; set; } = "{}";
    public string ContinuityVersions { get; set; } = "{}";
    public required string VisualFingerprint { get; set; }
    public decimal? CostUsd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}


public enum WorkJobStatus { Pending=0,Running=1,Completed=2,Failed=3,Cancelled=4 }
public enum WorkStepStatus { Pending=0,Ready=1,Leased=2,Completed=3,Retry=4,Failed=5,Cancelled=6 }

public sealed class WorkJobRecord
{
    public Guid Id{get;set;} public required string JobType{get;set;} public required string IdempotencyKey{get;set;}
    public WorkJobStatus Status{get;set;} public int Priority{get;set;} public string Payload{get;set;}="{}";
    public DateTimeOffset CreatedAt{get;set;} public DateTimeOffset UpdatedAt{get;set;} public DateTimeOffset? CompletedAt{get;set;}
}
public sealed class WorkStepRecord
{
    public Guid Id{get;set;} public Guid JobId{get;set;} public required string StepType{get;set;} public required string Queue{get;set;}
    public WorkStepStatus Status{get;set;} public int Priority{get;set;} public int AttemptCount{get;set;} public int MaxAttempts{get;set;}=5;
    public DateTimeOffset? NextEligibleAt{get;set;} public string Input{get;set;}="{}"; public string? Output{get;set;}
    public string? LeaseOwner{get;set;} public Guid? LeaseToken{get;set;} public DateTimeOffset? LeaseExpiresAt{get;set;}
    public DateTimeOffset? LastHeartbeatAt{get;set;} public string? ErrorCode{get;set;} public string? ErrorDetail{get;set;}
    public DateTimeOffset CreatedAt{get;set;} public DateTimeOffset UpdatedAt{get;set;} public DateTimeOffset? CompletedAt{get;set;}
}
public sealed class WorkStepDependencyRecord
{
    public Guid StepId{get;set;} public Guid DependsOnStepId{get;set;}
}
