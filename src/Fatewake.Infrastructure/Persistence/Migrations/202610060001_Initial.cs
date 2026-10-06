using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Fatewake.Infrastructure.Persistence.Migrations;

[DbContext(typeof(FatewakeDbContext))]
[Migration("20261006000100_Initial")]
public partial class Initial : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
CREATE TABLE account ("Id" uuid PRIMARY KEY, "DisplayName" text NULL, "PrimaryEmail" text NULL, "Status" text NOT NULL, "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL);
CREATE TABLE external_identity ("Id" uuid PRIMARY KEY, "AccountId" uuid NOT NULL REFERENCES account("Id"), "Provider" text NOT NULL, "ProviderSubject" text NOT NULL, "Email" text NULL, "EmailVerified" boolean NULL, "DisplayName" text NULL, "ClaimsSnapshot" jsonb NULL, "LinkedAt" timestamptz NOT NULL, "LastLoginAt" timestamptz NOT NULL, UNIQUE("Provider","ProviderSubject"));
CREATE INDEX ix_external_identity_account ON external_identity("AccountId");
CREATE TABLE realm ("Id" uuid PRIMARY KEY, "Key" text NOT NULL UNIQUE, "Name" text NOT NULL, "Properties" jsonb NOT NULL DEFAULT '{}');
CREATE TABLE timeline ("Id" uuid PRIMARY KEY, "RealmId" uuid NOT NULL REFERENCES realm("Id"), "ProgressionState" text NOT NULL, "ConvergenceState" text NOT NULL, "WorldClockPolicy" text NOT NULL, "CurrentSurvivorDay" integer NOT NULL DEFAULT 1, "NextEventSequence" bigint NOT NULL DEFAULT 1, "CreatedAt" timestamptz NOT NULL);
CREATE TABLE survivor ("Id" uuid PRIMARY KEY, "TimelineId" uuid NOT NULL REFERENCES timeline("Id"), "AccountId" uuid NULL, "DisplayName" text NOT NULL, "IdentityMode" text NOT NULL, "BroadRegion" text NOT NULL, "SurvivorDay" integer NOT NULL DEFAULT 1, "Status" text NOT NULL, "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL);
CREATE INDEX ix_survivor_timeline ON survivor("TimelineId");
CREATE TABLE event_instance ("Id" uuid PRIMARY KEY, "TimelineId" uuid NOT NULL REFERENCES timeline("Id"), "SurvivorId" uuid NOT NULL REFERENCES survivor("Id"), "EventKey" text NOT NULL, "Status" text NOT NULL, "SurvivorDay" integer NOT NULL, "Version" bigint NOT NULL DEFAULT 0, "State" jsonb NOT NULL DEFAULT '{}', "StartedAt" timestamptz NOT NULL, "ResolvedAt" timestamptz NULL);
CREATE TABLE action_attempt ("Id" uuid PRIMARY KEY, "IdempotencyKey" uuid NOT NULL, "EventInstanceId" uuid NOT NULL REFERENCES event_instance("Id"), "SurvivorId" uuid NOT NULL REFERENCES survivor("Id"), "InputKind" text NOT NULL, "RawInput" text NULL, "AuthoredActionKey" text NULL, "CandidateAction" jsonb NOT NULL, "SubmittedAt" timestamptz NOT NULL, UNIQUE("EventInstanceId","IdempotencyKey"));
CREATE TABLE action_resolution ("Id" uuid PRIMARY KEY, "ActionAttemptId" uuid NOT NULL UNIQUE REFERENCES action_attempt("Id"), "OutcomeType" text NOT NULL, "ResolvedAction" jsonb NOT NULL, "AuthoritativeEffects" jsonb NOT NULL, "NarrativeFacts" jsonb NOT NULL, "RulesVersion" text NOT NULL, "ResolvedAt" timestamptz NOT NULL);
CREATE TABLE game_event ("Id" uuid PRIMARY KEY, "TimelineId" uuid NOT NULL REFERENCES timeline("Id"), "SurvivorId" uuid NULL REFERENCES survivor("Id"), "EventType" text NOT NULL, "Sequence" bigint NOT NULL, "SurvivorDay" integer NOT NULL, "CausationEventId" uuid NULL REFERENCES game_event("Id"), "CorrelationId" uuid NOT NULL, "Payload" jsonb NOT NULL, "SchemaVersion" integer NOT NULL DEFAULT 1, "OccurredAt" timestamptz NOT NULL, UNIQUE("TimelineId","Sequence"));
CREATE TABLE wake ("Id" uuid PRIMARY KEY, "TimelineId" uuid NOT NULL REFERENCES timeline("Id"), "OriginEventId" uuid NOT NULL REFERENCES game_event("Id"), "WakeType" text NOT NULL, "Scope" text NOT NULL, "TargetEntityType" text NULL, "TargetEntityId" uuid NULL, "Severity" smallint NOT NULL, "State" text NOT NULL, "Properties" jsonb NOT NULL, "CreatedDay" integer NOT NULL, "CreatedAt" timestamptz NOT NULL);
CREATE INDEX ix_wake_origin ON wake("OriginEventId");
""");
    }
    protected override void Down(MigrationBuilder m) => m.Sql("""DROP TABLE IF EXISTS wake, game_event, action_resolution, action_attempt, event_instance, survivor, timeline, realm, external_identity, account CASCADE;""");
}
