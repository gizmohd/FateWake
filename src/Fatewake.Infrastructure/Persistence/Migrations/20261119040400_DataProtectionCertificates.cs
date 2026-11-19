using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

[DbContext(typeof(FatewakeDbContext))]
[Migration("20261119040400_DataProtectionCertificates")]
public sealed class DataProtectionCertificates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE data_protection_certificates (
            "Thumbprint" varchar(64) PRIMARY KEY,
            "EncryptedPfx" bytea NOT NULL,
            "NotBefore" timestamptz NOT NULL,
            "NotAfter" timestamptz NOT NULL,
            "RewrappedAt" timestamptz
        );
        CREATE INDEX "IX_data_protection_certificates_NotAfter" ON data_protection_certificates ("NotAfter");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("data_protection_certificates");
}
