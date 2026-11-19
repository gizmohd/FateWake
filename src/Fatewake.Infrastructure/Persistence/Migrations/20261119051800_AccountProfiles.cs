using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

[DbContext(typeof(FatewakeDbContext))]
[Migration("20261119051800_AccountProfiles")]
public sealed class AccountProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE account_profile (
            "AccountId" uuid PRIMARY KEY REFERENCES account ("Id") ON DELETE CASCADE,
            "FirstName" varchar(100) NULL,
            "LastName" varchar(100) NULL,
            "City" varchar(100) NULL,
            "State" varchar(100) NULL,
            "CompletedAt" timestamptz NULL
        );
        ALTER TABLE email_verification ADD COLUMN "FirstName" varchar(100) NULL;
        ALTER TABLE email_verification ADD COLUMN "LastName" varchar(100) NULL;
        ALTER TABLE email_verification ADD COLUMN "City" varchar(100) NULL;
        ALTER TABLE email_verification ADD COLUMN "State" varchar(100) NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE account_profile;
        ALTER TABLE email_verification DROP COLUMN "FirstName";
        ALTER TABLE email_verification DROP COLUMN "LastName";
        ALTER TABLE email_verification DROP COLUMN "City";
        ALTER TABLE email_verification DROP COLUMN "State";
        """);
}
