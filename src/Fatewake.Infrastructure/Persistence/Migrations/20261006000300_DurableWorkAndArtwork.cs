using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

/// <summary>Creates the authoritative work queue, artwork provenance, and survivor visual identity tables.</summary>
[DbContext(typeof(FatewakeDbContext))]
[Migration("20261006000300_DurableWorkAndArtwork")]
public sealed class DurableWorkAndArtwork : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
CREATE TABLE work_job (
    "Id" uuid PRIMARY KEY,
    "JobType" text NOT NULL,
    "IdempotencyKey" text NOT NULL,
    "Status" integer NOT NULL,
    "Priority" integer NOT NULL,
    "Payload" jsonb NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL,
    "CompletedAt" timestamptz NULL
);
CREATE UNIQUE INDEX "IX_work_job_JobType_IdempotencyKey" ON work_job ("JobType", "IdempotencyKey");
CREATE TABLE work_step (
    "Id" uuid PRIMARY KEY,
    "JobId" uuid NOT NULL REFERENCES work_job("Id") ON DELETE CASCADE,
    "StepType" text NOT NULL,
    "Queue" text NOT NULL,
    "Status" integer NOT NULL,
    "Priority" integer NOT NULL,
    "AttemptCount" integer NOT NULL,
    "MaxAttempts" integer NOT NULL,
    "NextEligibleAt" timestamptz NULL,
    "Input" jsonb NOT NULL,
    "Output" jsonb NULL,
    "LeaseOwner" text NULL,
    "LeaseToken" uuid NULL,
    "LeaseExpiresAt" timestamptz NULL,
    "LastHeartbeatAt" timestamptz NULL,
    "ErrorCode" text NULL,
    "ErrorDetail" text NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL,
    "CompletedAt" timestamptz NULL
);
CREATE INDEX "IX_work_step_JobId" ON work_step ("JobId");
CREATE INDEX "IX_work_step_Queue_Status_NextEligibleAt_Priority" ON work_step ("Queue", "Status", "NextEligibleAt", "Priority");
CREATE INDEX "IX_work_step_LeaseExpiresAt" ON work_step ("LeaseExpiresAt");
CREATE TABLE work_step_dependency (
    "StepId" uuid NOT NULL REFERENCES work_step("Id") ON DELETE CASCADE,
    "DependsOnStepId" uuid NOT NULL REFERENCES work_step("Id"),
    PRIMARY KEY ("StepId", "DependsOnStepId")
);
CREATE INDEX "IX_work_step_dependency_DependsOnStepId" ON work_step_dependency ("DependsOnStepId");
CREATE TABLE work_artifact (
    "Id" uuid PRIMARY KEY,
    "JobId" uuid NOT NULL REFERENCES work_job("Id") ON DELETE CASCADE,
    "Key" text NOT NULL,
    "Value" jsonb NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL
);
CREATE UNIQUE INDEX "IX_work_artifact_JobId_Key" ON work_artifact ("JobId", "Key");
CREATE TABLE art_asset (
    "Id" uuid PRIMARY KEY,
    "Key" text NOT NULL,
    "Version" integer NOT NULL,
    "AssetType" text NOT NULL,
    "Status" integer NOT NULL,
    "ParentAssetId" uuid NULL REFERENCES art_asset("Id"),
    "CharacterKey" text NULL,
    "LocationKey" text NULL,
    "VisualFingerprint" text NOT NULL,
    "ContentHash" text NOT NULL,
    "MasterPngStorageKey" text NOT NULL,
    "Width" integer NOT NULL,
    "Height" integer NOT NULL,
    "HasAlpha" boolean NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    "ApprovedAt" timestamptz NULL
);
CREATE UNIQUE INDEX "IX_art_asset_Key_Version" ON art_asset ("Key", "Version");
CREATE INDEX "IX_art_asset_ParentAssetId" ON art_asset ("ParentAssetId");
CREATE INDEX "IX_art_asset_VisualFingerprint" ON art_asset ("VisualFingerprint");
CREATE INDEX "IX_art_asset_ContentHash" ON art_asset ("ContentHash");
CREATE TABLE art_asset_derivative (
    "Id" uuid PRIMARY KEY,
    "ArtAssetId" uuid NOT NULL REFERENCES art_asset("Id") ON DELETE CASCADE,
    "Kind" integer NOT NULL,
    "StorageKey" text NOT NULL,
    "ContentType" text NOT NULL,
    "ContentHash" text NOT NULL,
    "Width" integer NOT NULL,
    "Height" integer NOT NULL,
    "ByteSize" bigint NOT NULL,
    "EncoderMetadata" text NULL,
    "CreatedAt" timestamptz NOT NULL
);
CREATE UNIQUE INDEX "IX_art_asset_derivative_ArtAssetId_Kind" ON art_asset_derivative ("ArtAssetId", "Kind");
CREATE TABLE art_generation (
    "Id" uuid PRIMARY KEY,
    "WorkJobId" uuid NULL REFERENCES work_job("Id") ON DELETE CASCADE,
    "ArtAssetId" uuid NULL REFERENCES art_asset("Id"),
    "IdempotencyKey" text NOT NULL,
    "Operation" text NOT NULL,
    "PromptKey" text NULL,
    "PromptTemplateVersion" text NULL,
    "ResolvedPrompt" text NOT NULL,
    "NegativePrompt" text NULL,
    "Provider" text NOT NULL,
    "Model" text NOT NULL,
    "ProviderJobId" text NULL,
    "PromptBuilderVersion" text NULL,
    "StyleBibleVersion" text NULL,
    "ReferenceAssets" jsonb NOT NULL,
    "GenerationParameters" jsonb NOT NULL,
    "ContinuityVersions" jsonb NOT NULL,
    "VisualFingerprint" text NOT NULL,
    "CostUsd" numeric NULL,
    "CreatedAt" timestamptz NOT NULL,
    "CompletedAt" timestamptz NULL
);
CREATE UNIQUE INDEX "IX_art_generation_WorkJobId" ON art_generation ("WorkJobId");
CREATE UNIQUE INDEX "IX_art_generation_IdempotencyKey" ON art_generation ("IdempotencyKey");
CREATE INDEX "IX_art_generation_ArtAssetId" ON art_generation ("ArtAssetId");
CREATE TABLE survivor_visual_identity (
    "SurvivorId" uuid PRIMARY KEY REFERENCES survivor("Id") ON DELETE CASCADE,
    "ActiveArtAssetId" uuid NOT NULL REFERENCES art_asset("Id"),
    "RequestedWorkJobId" uuid NULL,
    "RequestedVisualFingerprint" text NULL,
    "Status" integer NOT NULL,
    "SourceSurvivorId" uuid NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL,
    "ActivatedAt" timestamptz NULL
);
CREATE INDEX "IX_survivor_visual_identity_ActiveArtAssetId" ON survivor_visual_identity ("ActiveArtAssetId");
CREATE INDEX "IX_survivor_visual_identity_RequestedVisualFingerprint" ON survivor_visual_identity ("RequestedVisualFingerprint");
CREATE INDEX "IX_survivor_visual_identity_RequestedWorkJobId" ON survivor_visual_identity ("RequestedWorkJobId");
""");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder m)
    {
        m.Sql("""
DROP TABLE survivor_visual_identity;
DROP TABLE art_generation;
DROP TABLE art_asset_derivative;
DROP TABLE art_asset;
DROP TABLE work_artifact;
DROP TABLE work_step_dependency;
DROP TABLE work_step;
DROP TABLE work_job;
""");
    }
}
