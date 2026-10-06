using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

[DbContext(typeof(FatewakeDbContext))]
[Migration("20261006000500_EmailVerification")]
public sealed class EmailVerification : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
CREATE TABLE account_email (
    "AccountId" uuid PRIMARY KEY REFERENCES account("Id") ON DELETE CASCADE,
    "NormalizedEmail" varchar(254) NOT NULL,
    "Verified" boolean NOT NULL DEFAULT FALSE
);
CREATE UNIQUE INDEX "IX_account_email_NormalizedEmail" ON account_email ("NormalizedEmail");
INSERT INTO account_email ("AccountId", "NormalizedEmail", "Verified")
SELECT "AccountId", "NormalizedEmail", FALSE FROM local_credential;
CREATE TABLE email_verification (
    "Id" uuid PRIMARY KEY, "TokenHash" text NOT NULL, "Email" text NOT NULL,
    "NormalizedEmail" text NOT NULL, "PasswordHash" text NULL, "Provider" text NULL,
    "Subject" text NULL, "DisplayName" text NULL, "CreatedAt" timestamptz NOT NULL,
    "ExpiresAt" timestamptz NOT NULL, "ConsumedAt" timestamptz NULL
);
CREATE UNIQUE INDEX "IX_email_verification_TokenHash" ON email_verification ("TokenHash");
CREATE INDEX "IX_email_verification_NormalizedEmail" ON email_verification ("NormalizedEmail");
""");
    protected override void Down(MigrationBuilder m) => m.Sql("DROP TABLE email_verification; DROP TABLE account_email;");
}
