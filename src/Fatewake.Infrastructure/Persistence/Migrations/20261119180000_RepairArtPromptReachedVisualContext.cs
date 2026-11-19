using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

/// <summary>Repairs art prompt tables created by an earlier revision of the SQL prompt migration.</summary>
[DbContext(typeof(FatewakeDbContext))]
[Migration("20261119180000_RepairArtPromptReachedVisualContext")]
public sealed class RepairArtPromptReachedVisualContext : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE art_prompt ADD COLUMN IF NOT EXISTS "ReachedVisualContext" varchar(300) NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // SqlArtPrompts already declares the column. Rolling back this repair must preserve it and its data.
    }
}
