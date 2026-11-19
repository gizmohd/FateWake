using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

/// <summary>Creates OpenIddict authorization-server stores in a dedicated oidc schema.</summary>
[DbContext(typeof(FatewakeDbContext))]
[Migration("20261119040000_OpenIddictOidcSchema")]
public sealed class OpenIddictOidcSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder m) => m.Sql("""
CREATE SCHEMA IF NOT EXISTS oidc;

CREATE TABLE oidc."OpenIddictApplications" (
    "Id" text PRIMARY KEY,
    "ApplicationType" varchar(50) NULL,
    "ClientId" varchar(100) NULL,
    "ClientSecret" text NULL,
    "ClientType" varchar(50) NULL,
    "ConcurrencyToken" varchar(50) NULL,
    "ConsentType" varchar(50) NULL,
    "DisplayName" text NULL,
    "DisplayNames" text NULL,
    "JsonWebKeySet" text NULL,
    "Permissions" text NULL,
    "PostLogoutRedirectUris" text NULL,
    "Properties" text NULL,
    "RedirectUris" text NULL,
    "Requirements" text NULL,
    "Settings" text NULL
);
CREATE UNIQUE INDEX "IX_OpenIddictApplications_ClientId" ON oidc."OpenIddictApplications" ("ClientId");

CREATE TABLE oidc."OpenIddictScopes" (
    "Id" text PRIMARY KEY,
    "ConcurrencyToken" varchar(50) NULL,
    "Description" text NULL,
    "Descriptions" text NULL,
    "DisplayName" text NULL,
    "DisplayNames" text NULL,
    "Name" varchar(200) NULL,
    "Properties" text NULL,
    "Resources" text NULL
);
CREATE UNIQUE INDEX "IX_OpenIddictScopes_Name" ON oidc."OpenIddictScopes" ("Name");

CREATE TABLE oidc."OpenIddictAuthorizations" (
    "Id" text PRIMARY KEY,
    "ApplicationId" text NULL REFERENCES oidc."OpenIddictApplications"("Id"),
    "ConcurrencyToken" varchar(50) NULL,
    "CreationDate" timestamptz NULL,
    "Properties" text NULL,
    "Scopes" text NULL,
    "Status" varchar(50) NULL,
    "Subject" varchar(400) NULL,
    "Type" varchar(50) NULL
);
CREATE INDEX "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject" ON oidc."OpenIddictAuthorizations" ("ApplicationId", "Status", "Subject");

CREATE TABLE oidc."OpenIddictTokens" (
    "Id" text PRIMARY KEY,
    "ApplicationId" text NULL REFERENCES oidc."OpenIddictApplications"("Id"),
    "AuthorizationId" text NULL REFERENCES oidc."OpenIddictAuthorizations"("Id"),
    "ConcurrencyToken" varchar(50) NULL,
    "CreationDate" timestamptz NULL,
    "ExpirationDate" timestamptz NULL,
    "Payload" text NULL,
    "Properties" text NULL,
    "RedemptionDate" timestamptz NULL,
    "ReferenceId" varchar(100) NULL,
    "Status" varchar(50) NULL,
    "Subject" varchar(400) NULL,
    "Type" varchar(50) NULL
);
CREATE UNIQUE INDEX "IX_OpenIddictTokens_ReferenceId" ON oidc."OpenIddictTokens" ("ReferenceId");
CREATE INDEX "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type" ON oidc."OpenIddictTokens" ("ApplicationId", "Status", "Subject", "Type");
CREATE INDEX "IX_OpenIddictTokens_AuthorizationId" ON oidc."OpenIddictTokens" ("AuthorizationId");
CREATE INDEX "IX_OpenIddictTokens_ExpirationDate" ON oidc."OpenIddictTokens" ("ExpirationDate");
""");

    /// <inheritdoc />
    protected override void Down(MigrationBuilder m) => m.Sql("""
DROP TABLE oidc."OpenIddictTokens";
DROP TABLE oidc."OpenIddictAuthorizations";
DROP TABLE oidc."OpenIddictScopes";
DROP TABLE oidc."OpenIddictApplications";
DROP SCHEMA IF EXISTS oidc;
""");
}
