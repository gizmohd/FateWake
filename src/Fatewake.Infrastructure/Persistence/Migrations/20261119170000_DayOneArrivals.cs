using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

[DbContext(typeof(FatewakeDbContext))]
[Migration("20261119170000_DayOneArrivals")]
public sealed class DayOneArrivals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE survivor ADD "DayOneSiteKey" text NULL, ADD "DayOneStartMinute" integer NULL;
        ALTER TABLE survivor ADD CONSTRAINT ck_survivor_day_one_arrival CHECK (
            ("DayOneSiteKey" IS NULL AND "DayOneStartMinute" IS NULL) OR
            ("DayOneSiteKey" IS NOT NULL AND "DayOneStartMinute" IS NOT NULL AND
                "DayOneSiteKey" IN ('cedar-lane', 'ash-row', 'juniper-court') AND "DayOneStartMinute" BETWEEN 364 AND 377)
        );

        INSERT INTO world_cell ("Id", "RealmId", "Q", "R", "Name")
            SELECT seed.id::uuid, realm."Id", seed.q, seed.r, seed.name
            FROM realm CROSS JOIN (VALUES
                ('10000000-0000-0000-0001-000000000004', 0, 4, 'Ash Row duplex'),
                ('10000000-0000-0000-0001-000000000005', 1, 4, 'Ash Row porch'),
                ('10000000-0000-0000-0001-000000000006', 2, 4, 'Ash Row sidewalk'),
                ('10000000-0000-0000-0001-000000000007', 0, 8, 'Juniper Court rental'),
                ('10000000-0000-0000-0001-000000000008', 1, 8, 'Juniper Court entry'),
                ('10000000-0000-0000-0001-000000000009', 2, 8, 'Juniper courtyard')
            ) AS seed(id, q, r, name)
            WHERE realm."Key" = 'the-silence';
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE survivor DROP COLUMN "DayOneSiteKey", DROP COLUMN "DayOneStartMinute";
        DELETE FROM world_visit WHERE "CellId" IN (
            SELECT "Id" FROM world_cell WHERE "Id" BETWEEN
                '10000000-0000-0000-0001-000000000004' AND '10000000-0000-0000-0001-000000000009');
        DELETE FROM world_cell WHERE "Id" BETWEEN
            '10000000-0000-0000-0001-000000000004' AND '10000000-0000-0000-0001-000000000009';
        """);
}
