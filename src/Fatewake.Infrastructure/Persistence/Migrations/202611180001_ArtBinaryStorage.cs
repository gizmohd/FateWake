using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

/// <summary>Creates PostgreSQL storage for archival artwork binaries and derivatives.</summary>
[DbContext(typeof(FatewakeDbContext))]
[Migration("202611180001_ArtBinaryStorage")]
public sealed class ArtBinaryStorage : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder m) => m.Sql("""
ALTER TABLE art_asset ADD COLUMN "OwnerAccountId" uuid NULL REFERENCES account("Id") ON DELETE NO ACTION;
CREATE INDEX "IX_art_asset_OwnerAccountId" ON art_asset ("OwnerAccountId");
CREATE TABLE art_binary (
    "StorageKey" varchar(1024) PRIMARY KEY,
    "ContentType" varchar(128) NOT NULL,
    "ContentHash" varchar(64) NOT NULL,
    "Bytes" bytea NOT NULL,
    "ByteSize" bigint NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL,
    CONSTRAINT "CK_art_binary_ByteSize" CHECK ("ByteSize" = octet_length("Bytes"))
);
CREATE INDEX "IX_art_binary_ContentHash" ON art_binary ("ContentHash");
""");

    /// <inheritdoc />
    protected override void Down(MigrationBuilder m) => m.Sql("""
DROP TABLE art_binary;
DROP INDEX "IX_art_asset_OwnerAccountId";
ALTER TABLE art_asset DROP COLUMN "OwnerAccountId";
""");
}
