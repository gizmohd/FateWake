using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

/// <summary>Adds local email/password credentials without changing external identity records.</summary>
[DbContext(typeof(FatewakeDbContext))]
[Migration("20261006000400_LocalCredentials")]
public sealed class LocalCredentials : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder m) => m.Sql("""
CREATE TABLE local_credential (
    "AccountId" uuid PRIMARY KEY REFERENCES account("Id") ON DELETE CASCADE,
    "NormalizedEmail" varchar(254) NOT NULL,
    "PasswordHash" text NOT NULL,
    "FailedAttempts" integer NOT NULL DEFAULT 0,
    "LockoutEnd" timestamptz NULL
);
CREATE UNIQUE INDEX "IX_local_credential_NormalizedEmail" ON local_credential ("NormalizedEmail");
CREATE INDEX "IX_survivor_AccountId" ON survivor ("AccountId");
""");

    /// <inheritdoc />
    protected override void Down(MigrationBuilder m) => m.Sql("""
DROP TABLE local_credential;
DROP INDEX "IX_survivor_AccountId";
""");
}
