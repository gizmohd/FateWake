using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

/// <summary>Creates SQL-backed artwork prompt identities, immutable revisions, and review provenance links.</summary>
[DbContext(typeof(FatewakeDbContext))]
[Migration("20261119110000_SqlArtPrompts")]
public sealed class SqlArtPrompts : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
CREATE TABLE art_prompt (
    "Id" uuid PRIMARY KEY,
    "IdentityKey" varchar(300) NOT NULL,
    "AssetKey" varchar(100) NOT NULL,
    "ContextFingerprint" varchar(64) NOT NULL,
    "ReachedVisualContext" varchar(300) NULL,
    "StyleBibleVersion" varchar(20) NOT NULL,
    "OwnerAccountId" uuid NULL REFERENCES account("Id") ON DELETE NO ACTION,
    "IsPrivate" boolean NOT NULL,
    "Status" varchar(20) NOT NULL,
    "LatestRevisionNumber" integer NULL,
    "LatestRevisionId" uuid NULL,
    "CurrentRevisionNumber" integer NULL,
    "CurrentRevisionId" uuid NULL,
    "RequestedRevisionNumber" integer NULL,
    "RequestedRevisionId" uuid NULL,
    "RequestedAssetId" uuid NULL REFERENCES art_asset("Id") ON DELETE NO ACTION,
    "RequestedWorkJobId" uuid NULL REFERENCES work_job("Id") ON DELETE NO ACTION,
    "AuthoringJobId" uuid NULL REFERENCES work_job("Id") ON DELETE NO ACTION,
    "FailureCode" varchar(40) NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL
);
CREATE UNIQUE INDEX "IX_art_prompt_IdentityKey" ON art_prompt ("IdentityKey");
CREATE INDEX "IX_art_prompt_AuthoringJobId" ON art_prompt ("AuthoringJobId");
CREATE INDEX "IX_art_prompt_LatestRevisionId" ON art_prompt ("LatestRevisionId");
CREATE INDEX "IX_art_prompt_RequestedRevisionId" ON art_prompt ("RequestedRevisionId");
CREATE INDEX "IX_art_prompt_RequestedAssetId" ON art_prompt ("RequestedAssetId");
CREATE INDEX "IX_art_prompt_RequestedWorkJobId" ON art_prompt ("RequestedWorkJobId");
CREATE INDEX "IX_art_prompt_OwnerAccountId" ON art_prompt ("OwnerAccountId");

CREATE TABLE art_prompt_revision (
    "Id" uuid PRIMARY KEY,
    "ArtPromptId" uuid NOT NULL REFERENCES art_prompt("Id") ON DELETE CASCADE,
    "RevisionNumber" integer NOT NULL,
    "ParentRevisionId" uuid NULL REFERENCES art_prompt_revision("Id") ON DELETE NO ACTION,
    "PositivePrompt" varchar(8000) NOT NULL,
    "NegativePrompt" varchar(2000) NULL,
    "Source" varchar(20) NOT NULL,
    "CreatorAccountId" uuid NULL REFERENCES account("Id") ON DELETE NO ACTION,
    "Model" varchar(100) NULL,
    "RequestContextFingerprint" varchar(64) NULL,
    "CreatedAt" timestamptz NOT NULL
);
CREATE UNIQUE INDEX "IX_art_prompt_revision_ArtPromptId_RevisionNumber"
    ON art_prompt_revision ("ArtPromptId", "RevisionNumber");
CREATE INDEX "IX_art_prompt_revision_ParentRevisionId" ON art_prompt_revision ("ParentRevisionId");
CREATE INDEX "IX_art_prompt_revision_CreatorAccountId" ON art_prompt_revision ("CreatorAccountId");

ALTER TABLE art_prompt ADD CONSTRAINT "FK_art_prompt_LatestRevisionId"
    FOREIGN KEY ("LatestRevisionId") REFERENCES art_prompt_revision("Id") ON DELETE NO ACTION;
ALTER TABLE art_prompt ADD CONSTRAINT "FK_art_prompt_CurrentRevisionId"
    FOREIGN KEY ("CurrentRevisionId") REFERENCES art_prompt_revision("Id") ON DELETE NO ACTION;
ALTER TABLE art_prompt ADD CONSTRAINT "FK_art_prompt_RequestedRevisionId"
    FOREIGN KEY ("RequestedRevisionId") REFERENCES art_prompt_revision("Id") ON DELETE NO ACTION;

ALTER TABLE art_generation ADD COLUMN "PromptRevisionId" uuid NULL
    REFERENCES art_prompt_revision("Id") ON DELETE NO ACTION;
CREATE INDEX "IX_art_generation_PromptRevisionId" ON art_generation ("PromptRevisionId");

ALTER TABLE art_review ADD COLUMN "SourceArtAssetId" uuid NULL REFERENCES art_asset("Id") ON DELETE NO ACTION;
ALTER TABLE art_review ADD COLUMN "PromptRevisionId" uuid NULL REFERENCES art_prompt_revision("Id") ON DELETE NO ACTION;
ALTER TABLE art_review ADD COLUMN "RegenerationWorkJobId" uuid NULL REFERENCES work_job("Id") ON DELETE NO ACTION;
CREATE INDEX "IX_art_review_SourceArtAssetId" ON art_review ("SourceArtAssetId");
CREATE INDEX "IX_art_review_PromptRevisionId" ON art_review ("PromptRevisionId");
CREATE INDEX "IX_art_review_RegenerationWorkJobId" ON art_review ("RegenerationWorkJobId");
""");

    protected override void Down(MigrationBuilder m) => m.Sql("""
DROP INDEX IF EXISTS "IX_art_review_RegenerationWorkJobId";
DROP INDEX IF EXISTS "IX_art_review_PromptRevisionId";
DROP INDEX IF EXISTS "IX_art_review_SourceArtAssetId";
ALTER TABLE art_review DROP COLUMN "RegenerationWorkJobId";
ALTER TABLE art_review DROP COLUMN "PromptRevisionId";
ALTER TABLE art_review DROP COLUMN "SourceArtAssetId";
DROP INDEX IF EXISTS "IX_art_generation_PromptRevisionId";
ALTER TABLE art_generation DROP COLUMN "PromptRevisionId";
ALTER TABLE art_prompt DROP CONSTRAINT "FK_art_prompt_RequestedRevisionId";
ALTER TABLE art_prompt DROP CONSTRAINT "FK_art_prompt_CurrentRevisionId";
ALTER TABLE art_prompt DROP CONSTRAINT "FK_art_prompt_LatestRevisionId";
DROP TABLE art_prompt_revision;
DROP TABLE art_prompt;
""");
}
