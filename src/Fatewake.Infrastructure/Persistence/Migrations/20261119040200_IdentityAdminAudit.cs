using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

[DbContext(typeof(FatewakeDbContext))]
[Migration("20261119040200_IdentityAdminAudit")]
public sealed class IdentityAdminAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE account ADD COLUMN "AccessRevokedAt" timestamptz;
        DROP INDEX "IX_account_role_single_admin";
        CREATE TABLE identity_admin_audit (
            id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            actor uuid NOT NULL,
            target text NOT NULL,
            action text NOT NULL,
            result text NOT NULL,
            occurred_at timestamptz NOT NULL,
            correlation text NOT NULL
        );
        CREATE TABLE identity_admin_proof (
            token_hash text PRIMARY KEY,
            account_id uuid NOT NULL REFERENCES account ("Id"),
            purpose text NOT NULL,
            email text NOT NULL,
            normalized_email text NOT NULL,
            expires_at timestamptz NOT NULL,
            UNIQUE (account_id, purpose)
        );
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE UNIQUE INDEX "IX_account_role_single_admin" ON account_role ("Role") WHERE "Role" = 'Admin';
        DROP TABLE identity_admin_proof;
        DROP TABLE identity_admin_audit;
        ALTER TABLE account DROP COLUMN "AccessRevokedAt";
        """);
}
