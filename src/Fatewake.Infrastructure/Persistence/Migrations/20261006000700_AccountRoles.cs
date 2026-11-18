using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Fatewake.Infrastructure.Persistence.Migrations;
/// <summary>Adds persisted sign-in roles with a single initial administrator invariant.</summary>
[DbContext(typeof(FatewakeDbContext))]
[Migration("20261006000700_AccountRoles")]
public sealed class AccountRoles : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder m) => m.Sql("""
CREATE TABLE account_role (
 "AccountId" uuid PRIMARY KEY REFERENCES account("Id") ON DELETE NO ACTION,
 "Role" varchar(10) NOT NULL CHECK ("Role" IN ('Admin', 'User')),
 "AssignedAt" timestamptz NOT NULL
);
CREATE UNIQUE INDEX "IX_account_role_single_admin" ON account_role ("Role") WHERE "Role" = 'Admin';
""");
  /// <inheritdoc />
  protected override void Down(MigrationBuilder m) => m.Sql("DROP TABLE account_role;");
}
