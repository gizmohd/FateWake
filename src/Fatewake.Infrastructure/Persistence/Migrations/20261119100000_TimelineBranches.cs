using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

[DbContext(typeof(FatewakeDbContext))]
[Migration("20261119100000_TimelineBranches")]
public sealed class TimelineBranches : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE survivor_timeline_state (
            "SurvivorId" uuid NOT NULL REFERENCES survivor ("Id"),
            "TimelineId" uuid NOT NULL REFERENCES timeline ("Id"),
            "SurvivorDay" integer NOT NULL,
            "Status" integer NOT NULL,
            "Facts" jsonb NOT NULL DEFAULT '{}',
            "Version" bigint NOT NULL DEFAULT 0,
            PRIMARY KEY ("SurvivorId", "TimelineId")
        );
        INSERT INTO survivor_timeline_state ("SurvivorId", "TimelineId", "SurvivorDay", "Status", "Facts")
        SELECT s."Id", s."TimelineId", s."SurvivorDay", s."Status",
            COALESCE((
                SELECT jsonb_object_agg(effects.key, effects.value) FROM (
                    SELECT DISTINCT ON (effect->>'Key') effect->>'Key' AS key, effect->>'Value' AS value
                    FROM action_attempt a JOIN action_resolution r ON r."ActionAttemptId" = a."Id"
                    JOIN event_instance e ON e."Id" = a."EventInstanceId"
                    CROSS JOIN LATERAL jsonb_array_elements(r."AuthoritativeEffects") effect
                    WHERE a."SurvivorId" = s."Id" AND e."TimelineId" = s."TimelineId"
                    ORDER BY effect->>'Key', r."ResolvedAt" DESC, r."Id" DESC
                ) effects
            ), '{}'::jsonb)
        FROM survivor s;
        CREATE TABLE timeline_checkpoint (
            "Id" uuid PRIMARY KEY,
            "TimelineId" uuid NOT NULL REFERENCES timeline ("Id"),
            "SurvivorId" uuid NOT NULL REFERENCES survivor ("Id"),
            "EventInstanceId" uuid NOT NULL REFERENCES event_instance ("Id"),
            "Key" varchar(100) NOT NULL,
            "Label" varchar(200) NOT NULL,
            "TimeLabel" varchar(100) NOT NULL,
            "Snapshot" jsonb NOT NULL,
            "SnapshotHash" varchar(64) NOT NULL,
            "CreatedAt" timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX "IX_timeline_checkpoint_TimelineId_EventInstanceId_Key"
            ON timeline_checkpoint ("TimelineId", "EventInstanceId", "Key");
        CREATE INDEX "IX_timeline_checkpoint_SurvivorId_CreatedAt" ON timeline_checkpoint ("SurvivorId", "CreatedAt");
        CREATE TABLE timeline_branch (
            "TimelineId" uuid PRIMARY KEY REFERENCES timeline ("Id"),
            "SurvivorId" uuid NOT NULL REFERENCES survivor ("Id"),
            "RootTimelineId" uuid NOT NULL REFERENCES timeline ("Id"),
            "ParentTimelineId" uuid NULL REFERENCES timeline ("Id"),
            "CheckpointId" uuid NULL REFERENCES timeline_checkpoint ("Id"),
            "RequestId" uuid NULL,
            "Origin" varchar(20) NOT NULL,
            "CreatedAt" timestamptz NOT NULL
        );
        CREATE INDEX "IX_timeline_branch_SurvivorId_CreatedAt" ON timeline_branch ("SurvivorId", "CreatedAt");
        CREATE UNIQUE INDEX "IX_timeline_branch_SurvivorId_RequestId" ON timeline_branch ("SurvivorId", "RequestId");
        INSERT INTO timeline_branch ("TimelineId", "SurvivorId", "RootTimelineId", "Origin", "CreatedAt")
            SELECT s."TimelineId", s."Id", s."TimelineId", 'original', t."CreatedAt"
            FROM survivor s JOIN timeline t ON t."Id" = s."TimelineId"
            WHERE NOT EXISTS (
                SELECT 1 FROM survivor other
                WHERE other."TimelineId" = s."TimelineId" AND other."Id" <> s."Id"
            );
        DROP INDEX "IX_story_panel_SurvivorId_EntryKey";
        CREATE UNIQUE INDEX "IX_story_panel_SurvivorId_TimelineId_EntryKey"
            ON story_panel ("SurvivorId", "TimelineId", "EntryKey");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$ BEGIN
            IF EXISTS (SELECT 1 FROM timeline_branch WHERE "ParentTimelineId" IS NOT NULL) THEN
                RAISE EXCEPTION 'Cannot remove timeline support while preserved branches exist.';
            END IF;
        END $$;
        DROP TABLE timeline_branch;
        DROP TABLE timeline_checkpoint;
        DROP TABLE survivor_timeline_state;
        DROP INDEX "IX_story_panel_SurvivorId_TimelineId_EntryKey";
        CREATE UNIQUE INDEX "IX_story_panel_SurvivorId_EntryKey" ON story_panel ("SurvivorId", "EntryKey");
        """);
}
