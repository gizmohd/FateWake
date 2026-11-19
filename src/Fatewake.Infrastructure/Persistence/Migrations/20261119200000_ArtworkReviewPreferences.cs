using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

/// <summary>Stores the reviewer's choice to reuse a critique for future artwork.</summary>
[DbContext(typeof(FatewakeDbContext))]
[Migration("20261119200000_ArtworkReviewPreferences")]
public sealed class ArtworkReviewPreferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE art_review ADD COLUMN "UseForFutureArtwork" boolean NOT NULL DEFAULT false;
        CREATE INDEX ix_art_review_visual_preferences ON art_review ("CreatedAt" DESC, "Id")
            WHERE "UseForFutureArtwork";
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP INDEX ix_art_review_visual_preferences;
        ALTER TABLE art_review DROP COLUMN "UseForFutureArtwork";
        """);
}
