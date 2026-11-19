using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

/// <summary>Creates the source-grounded RAG corpus, extracted claims, full-text search, and pgvector indexes.</summary>
[DbContext(typeof(FatewakeDbContext))]
[Migration("202611190002_RagKnowledge")]
public sealed class RagKnowledge : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
CREATE EXTENSION IF NOT EXISTS vector;
CREATE TABLE rag_source (
    "Id" uuid PRIMARY KEY,
    "SourceKey" text NOT NULL,
    "SourceType" text NOT NULL,
    "Title" text NOT NULL,
    "Origin" text NOT NULL,
    "CanonicalUri" text NULL,
    "ContentHash" text NOT NULL,
    "LicenseStatus" text NOT NULL,
    "ReviewStatus" text NOT NULL,
    "Visibility" text NOT NULL,
    "Metadata" jsonb NOT NULL DEFAULT '{}'::jsonb,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL
);
CREATE UNIQUE INDEX "IX_rag_source_SourceKey" ON rag_source ("SourceKey");
CREATE INDEX "IX_rag_source_SourceType_ReviewStatus" ON rag_source ("SourceType", "ReviewStatus");
CREATE TABLE rag_document (
    "Id" uuid PRIMARY KEY,
    "SourceId" uuid NOT NULL REFERENCES rag_source("Id") ON DELETE CASCADE,
    "DocumentKey" text NOT NULL,
    "DocumentType" text NOT NULL,
    "Title" text NOT NULL,
    "Language" text NOT NULL,
    "Sequence" integer NOT NULL,
    "ContentHash" text NOT NULL,
    "Metadata" jsonb NOT NULL DEFAULT '{}'::jsonb,
    "Status" text NOT NULL,
    "PublishedAt" timestamptz NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL
);
CREATE UNIQUE INDEX "IX_rag_document_SourceId_DocumentKey" ON rag_document ("SourceId", "DocumentKey");
CREATE INDEX "IX_rag_document_DocumentType_Status" ON rag_document ("DocumentType", "Status");
CREATE TABLE rag_chunk (
    "Id" uuid PRIMARY KEY,
    "DocumentId" uuid NOT NULL REFERENCES rag_document("Id") ON DELETE CASCADE,
    "ChunkKey" text NOT NULL,
    "Ordinal" integer NOT NULL,
    "HeadingPath" text NULL,
    "Text" text NOT NULL,
    "StartOffset" integer NULL,
    "EndOffset" integer NULL,
    "TimelineKey" text NULL,
    "SurvivorDayMin" integer NULL,
    "SurvivorDayMax" integer NULL,
    "SpoilerSeriesMin" integer NULL,
    "SpoilerSeriesMax" integer NULL,
    "CanonStatus" text NOT NULL,
    "ReviewStatus" text NOT NULL,
    "EmbeddingModel" text NULL,
    "EmbeddingDimensions" integer NULL,
    "Embedding" vector(1024) NULL,
    "SearchVector" tsvector GENERATED ALWAYS AS (to_tsvector('english', "Text")) STORED,
    "EmbeddedAt" timestamptz NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL
);
CREATE UNIQUE INDEX "IX_rag_chunk_DocumentId_Ordinal" ON rag_chunk ("DocumentId", "Ordinal");
CREATE UNIQUE INDEX "IX_rag_chunk_ChunkKey" ON rag_chunk ("ChunkKey");
CREATE INDEX "IX_rag_chunk_TimelineDay" ON rag_chunk ("TimelineKey", "SurvivorDayMin", "SurvivorDayMax");
CREATE INDEX "IX_rag_chunk_SpoilerSeries" ON rag_chunk ("SpoilerSeriesMin", "SpoilerSeriesMax");
CREATE INDEX "IX_rag_chunk_SearchVector" ON rag_chunk USING GIN ("SearchVector");
CREATE INDEX "IX_rag_chunk_Embedding" ON rag_chunk USING HNSW ("Embedding" vector_cosine_ops);
CREATE TABLE rag_claim (
    "Id" uuid PRIMARY KEY,
    "SourceChunkId" uuid NULL REFERENCES rag_chunk("Id") ON DELETE SET NULL,
    "SourceDocumentId" uuid NULL REFERENCES rag_document("Id") ON DELETE SET NULL,
    "ClaimType" text NOT NULL,
    "Subject" text NOT NULL,
    "Predicate" text NULL,
    "Object" text NULL,
    "Confidence" numeric(5,4) NOT NULL,
    "Status" text NOT NULL,
    "ApprovedBy" text NULL,
    "ApprovedAt" timestamptz NULL,
    "Metadata" jsonb NOT NULL DEFAULT '{}'::jsonb,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL
);
CREATE INDEX "IX_rag_claim_Subject_Status" ON rag_claim ("Subject", "Status");
CREATE INDEX "IX_rag_claim_SourceChunkId" ON rag_claim ("SourceChunkId");
CREATE INDEX "IX_rag_claim_SourceDocumentId" ON rag_claim ("SourceDocumentId");
CREATE TABLE rag_ingest_run (
    "Id" uuid PRIMARY KEY,
    "RunKey" text NOT NULL,
    "Phase" text NOT NULL,
    "Status" text NOT NULL,
    "IngestorVersion" text NULL,
    "EmbeddingModel" text NULL,
    "EmbeddingDimensions" integer NULL,
    "SourceCount" integer NOT NULL,
    "DocumentCount" integer NOT NULL,
    "ChunkCount" integer NOT NULL,
    "ErrorDetail" text NULL,
    "StartedAt" timestamptz NOT NULL,
    "CompletedAt" timestamptz NULL
);
CREATE UNIQUE INDEX "IX_rag_ingest_run_RunKey" ON rag_ingest_run ("RunKey");
CREATE INDEX "IX_rag_ingest_run_Status_StartedAt" ON rag_ingest_run ("Status", "StartedAt");
CREATE TABLE rag_staging (
    "Id" uuid PRIMARY KEY,
    "ContentHash" text NOT NULL,
    "FileName" varchar(1024) NOT NULL,
    "ContentType" varchar(128) NOT NULL,
    "Bytes" bytea NOT NULL,
    "ByteSize" bigint NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    CONSTRAINT "CK_rag_staging_ByteSize" CHECK ("ByteSize" = octet_length("Bytes"))
);
CREATE UNIQUE INDEX "IX_rag_staging_ContentHash_FileName" ON rag_staging ("ContentHash", "FileName");
""");

    protected override void Down(MigrationBuilder m) => m.Sql("""
DROP TABLE rag_staging;
DROP TABLE rag_ingest_run;
DROP TABLE rag_claim;
DROP TABLE rag_chunk;
DROP TABLE rag_document;
DROP TABLE rag_source;
""");
}
