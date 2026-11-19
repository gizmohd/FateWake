using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

[DbContext(typeof(FatewakeDbContext))]
[Migration("20261119120000_PathweaverBrand")]
public sealed class PathweaverBrand : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE oidc."OpenIddictApplications" SET "DisplayName" = 'Pathweaver Web'
            WHERE "ClientId" = 'fatewake-web' AND "DisplayName" = 'Fatewake Web';
        UPDATE oidc."OpenIddictApplications" SET "DisplayName" = 'Pathweaver API'
            WHERE "ClientId" = 'fatewake-api' AND "DisplayName" = 'Fatewake API';
        UPDATE oidc."OpenIddictScopes" SET "DisplayName" = 'Pathweaver API'
            WHERE "Name" = 'fatewake-api' AND "DisplayName" = 'Fatewake API';
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE oidc."OpenIddictApplications" SET "DisplayName" = 'Fatewake Web'
            WHERE "ClientId" = 'fatewake-web' AND "DisplayName" = 'Pathweaver Web';
        UPDATE oidc."OpenIddictApplications" SET "DisplayName" = 'Fatewake API'
            WHERE "ClientId" = 'fatewake-api' AND "DisplayName" = 'Pathweaver API';
        UPDATE oidc."OpenIddictScopes" SET "DisplayName" = 'Fatewake API'
            WHERE "Name" = 'fatewake-api' AND "DisplayName" = 'Pathweaver API';
        """);
}
