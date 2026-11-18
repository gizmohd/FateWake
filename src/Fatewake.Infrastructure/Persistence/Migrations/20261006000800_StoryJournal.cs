using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Fatewake.Infrastructure.Persistence.Migrations;
/// <summary>Adds chronological seen panels and controlled external-influence cutscenes.</summary>
[DbContext(typeof(FatewakeDbContext))]
[Migration("20261006000800_StoryJournal")]
public sealed class StoryJournal : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder m) => m.Sql("""
CREATE TABLE story_panel (
 "Id" uuid PRIMARY KEY, "SurvivorId" uuid NOT NULL REFERENCES survivor("Id"),
 "TimelineId" uuid NOT NULL REFERENCES timeline("Id"), "EventInstanceId" uuid NULL REFERENCES event_instance("Id"),
 "SourceEventId" uuid NULL REFERENCES game_event("Id"), "EntryKey" varchar(200) NOT NULL,
 "Sequence" bigint NOT NULL, "SurvivorDay" integer NOT NULL, "Kind" varchar(30) NOT NULL,
 "Title" text NOT NULL, "TimeLabel" text NULL, "Narration" text NOT NULL,
 "DialogueSpeaker" text NULL, "DialogueText" text NULL, "Layers" jsonb NOT NULL,
 "HiddenCauseNarration" text NULL, "CauseRevealed" boolean NOT NULL,
 "VisibleFromDay" integer NOT NULL, "Reconstructed" boolean NOT NULL, "RecordedAt" timestamptz NOT NULL
);
CREATE UNIQUE INDEX "IX_story_panel_SurvivorId_EntryKey" ON story_panel ("SurvivorId","EntryKey");
CREATE UNIQUE INDEX "IX_story_panel_TimelineId_Sequence" ON story_panel ("TimelineId","Sequence");
""");
    /// <inheritdoc />
    protected override void Down(MigrationBuilder m) => m.Sql("DROP TABLE story_panel;");
}
