using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

[DbContext(typeof(FatewakeDbContext))]
[Migration("20261119040100_OpenIddictTokenTypeLength")]
public sealed class OpenIddictTokenTypeLength : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""ALTER TABLE oidc."OpenIddictTokens" ALTER COLUMN "Type" TYPE varchar(150);""");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""ALTER TABLE oidc."OpenIddictTokens" ALTER COLUMN "Type" TYPE varchar(50);""");
}
