using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

[DbContext(typeof(FatewakeDbContext))]
[Migration("20261119130000_GeneratedScenes")]
public sealed class GeneratedScenes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE generated_scene (
            "Id" uuid PRIMARY KEY,
            "SurvivorId" uuid NOT NULL REFERENCES survivor ("Id"),
            "TimelineId" uuid NOT NULL REFERENCES timeline ("Id"),
            "Sequence" integer NOT NULL,
            "Status" varchar(20) NOT NULL,
            "SurvivorDay" integer NOT NULL,
            "Title" varchar(200) NULL,
            "Summary" text NULL,
            "Spec" jsonb NULL,
            "HasDecision" boolean NOT NULL,
            "DecisionFreeStreak" integer NOT NULL,
            "Error" text NULL,
            "RagSourceId" uuid NULL,
            "CreatedAt" timestamptz NOT NULL,
            "UpdatedAt" timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX "IX_generated_scene_SurvivorId_TimelineId_Sequence"
            ON generated_scene ("SurvivorId", "TimelineId", "Sequence");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE generated_scene;
        """);
}
