using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Fatewake.Infrastructure.Persistence.Migrations;
/// <summary>Stores administrator artwork critiques and correction requests.</summary>
[DbContext(typeof(FatewakeDbContext))]
[Migration("20261006000600_ArtReviews")]
public sealed class ArtReviews : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder m) => m.Sql("""
CREATE TABLE art_review (
 "Id" uuid PRIMARY KEY, "AccountId" uuid NOT NULL REFERENCES account("Id"),
 "SceneKey" varchar(100) NOT NULL, "AssetKey" varchar(100) NULL, "Version" integer NULL,
 "CompositionVersions" jsonb NOT NULL, "Critique" varchar(4000) NOT NULL,
 "Correction" varchar(4000) NULL, "Regenerate" boolean NOT NULL, "CreatedAt" timestamptz NOT NULL
);
CREATE INDEX "IX_art_review_SceneKey_CreatedAt" ON art_review ("SceneKey", "CreatedAt");
CREATE INDEX "IX_art_review_AssetKey_CreatedAt" ON art_review ("AssetKey", "CreatedAt");
""");
    /// <inheritdoc />
    protected override void Down(MigrationBuilder m) => m.Sql("DROP TABLE art_review;");
}
