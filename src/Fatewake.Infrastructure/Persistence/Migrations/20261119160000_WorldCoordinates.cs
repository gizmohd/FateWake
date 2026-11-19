using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

[DbContext(typeof(FatewakeDbContext))]
[Migration("20261119160000_WorldCoordinates")]
public sealed class WorldCoordinates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE world_cell (
            "Id" uuid PRIMARY KEY,
            "RealmId" uuid NOT NULL REFERENCES realm ("Id"),
            "Q" integer NOT NULL,
            "R" integer NOT NULL,
            "Name" text NOT NULL,
            UNIQUE ("RealmId", "Q", "R")
        );
        CREATE TABLE world_visit (
            "SurvivorId" uuid NOT NULL REFERENCES survivor ("Id"),
            "TimelineId" uuid NOT NULL REFERENCES timeline ("Id"),
            "Sequence" bigint NOT NULL CHECK ("Sequence" > 0),
            "CellId" uuid NOT NULL REFERENCES world_cell ("Id"),
            "ClockKey" varchar(80) NOT NULL CHECK (length("ClockKey") > 0),
            "Tick" bigint NOT NULL CHECK ("Tick" >= 0),
            "Authored" boolean NOT NULL,
            PRIMARY KEY ("SurvivorId", "TimelineId", "Sequence")
        );
        CREATE INDEX ix_world_visit_position ON world_visit ("TimelineId", "CellId", "ClockKey", "Tick");

        INSERT INTO realm ("Id", "Key", "Name", "Properties")
            VALUES ('10000000-0000-0000-0000-000000000001', 'the-silence', 'The Silence', '{}')
            ON CONFLICT ("Key") DO NOTHING;

        INSERT INTO world_cell ("Id", "RealmId", "Q", "R", "Name")
            SELECT seed.id::uuid, realm."Id", seed.q, 0, seed.name
            FROM realm CROSS JOIN (VALUES
                ('10000000-0000-0000-0001-000000000001', 0, 'Home'),
                ('10000000-0000-0000-0001-000000000002', 1, 'Doorway'),
                ('10000000-0000-0000-0001-000000000003', 2, 'Residential street')
            ) AS seed(id, q, name)
            WHERE realm."Key" = 'the-silence';
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE world_visit;
        DROP TABLE world_cell;
        """);
}
