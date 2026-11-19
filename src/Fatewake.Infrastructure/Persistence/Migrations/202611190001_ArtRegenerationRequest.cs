using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

/// <summary>Creates the durable admin-visible queue for artwork that should replace a fallback generation.</summary>
[DbContext(typeof(FatewakeDbContext))]
[Migration("202611190001_ArtRegenerationRequest")]
public sealed class ArtRegenerationRequest : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
CREATE TABLE art_regeneration_request (
    "Id" uuid PRIMARY KEY,
    "SourceWorkJobId" uuid NULL REFERENCES work_job("Id") ON DELETE CASCADE,
    "ArtAssetId" uuid NULL REFERENCES art_asset("Id"),
    "VisualFingerprint" varchar(1024) NOT NULL,
    "RequestJson" jsonb NOT NULL,
    "TargetProvider" varchar(50) NOT NULL,
    "TargetModel" text NULL,
    "Status" varchar(20) NOT NULL,
    "Reason" text NULL,
    "RegenerationWorkJobId" uuid NULL,
    "AttemptCount" integer NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL,
    "NextEligibleAt" timestamptz NULL
);
CREATE UNIQUE INDEX "IX_art_regeneration_request_SourceWorkJobId" ON art_regeneration_request ("SourceWorkJobId");
CREATE INDEX "IX_art_regeneration_request_Status_NextEligibleAt" ON art_regeneration_request ("Status", "NextEligibleAt");
CREATE INDEX "IX_art_regeneration_request_ArtAssetId" ON art_regeneration_request ("ArtAssetId");
""");

    protected override void Down(MigrationBuilder m) => m.Sql("""DROP TABLE art_regeneration_request;""");
}
