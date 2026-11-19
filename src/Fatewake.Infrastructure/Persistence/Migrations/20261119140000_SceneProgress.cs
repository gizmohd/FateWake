using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

[DbContext(typeof(FatewakeDbContext))]
[Migration("20261119140000_SceneProgress")]
public sealed class SceneProgress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE generated_scene
            ADD "CompletedAt" timestamptz NULL,
            ADD "ChosenActionType" varchar(40) NULL,
            ADD "ChosenText" varchar(500) NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE generated_scene DROP COLUMN "CompletedAt", DROP COLUMN "ChosenActionType", DROP COLUMN "ChosenText";
        """);
}
