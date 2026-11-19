using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

[DbContext(typeof(FatewakeDbContext))]
[Migration("20261119150000_ContentPreferencesAndStoryBranches")]
public sealed class ContentPreferencesAndStoryBranches : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE account_profile ADD "BirthDate" date NULL, ADD "ContentLevel" integer NULL;
        ALTER TABLE email_verification ADD "BirthDate" date NULL;
        ALTER TABLE generated_scene
            ADD "ContentLevel" integer NULL,
            ADD "BranchKey" text NULL,
            ADD "BranchFocus" text NULL;
        CREATE INDEX ix_generated_scene_branch ON generated_scene ("BranchKey", "ContentLevel")
            WHERE "BranchKey" IS NOT NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP INDEX ix_generated_scene_branch;
        ALTER TABLE generated_scene DROP COLUMN "ContentLevel", DROP COLUMN "BranchKey", DROP COLUMN "BranchFocus";
        ALTER TABLE email_verification DROP COLUMN "BirthDate";
        ALTER TABLE account_profile DROP COLUMN "BirthDate", DROP COLUMN "ContentLevel";
        """);
}
