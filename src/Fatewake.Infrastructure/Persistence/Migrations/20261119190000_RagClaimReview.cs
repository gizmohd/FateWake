using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

[DbContext(typeof(FatewakeDbContext))]
[Migration("20261119190000_RagClaimReview")]
public sealed class RagClaimReview : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE rag_chunk ADD COLUMN "ClaimsExtractedAt" timestamptz,
                             ADD COLUMN "ClaimExtractionLeaseUntil" timestamptz,
                             ADD COLUMN "ClaimExtractionLeaseId" uuid,
                             ADD COLUMN "EmbeddingLeaseUntil" timestamptz,
                             ADD COLUMN "EmbeddingLeaseId" uuid;
        ALTER TABLE rag_source ADD COLUMN "ClaimAutoApprovalMinimumConfidence" numeric(5,4)
            CHECK ("ClaimAutoApprovalMinimumConfidence" BETWEEN 0 AND 1);
        ALTER TABLE rag_claim ADD COLUMN "Evidence" text,
                             ADD COLUMN "ReviewedBy" text,
                             ADD COLUMN "ReviewedAt" timestamptz;
        ALTER TABLE rag_staging ADD COLUMN "SourceKey" text NOT NULL DEFAULT '';
        DROP INDEX "IX_rag_staging_ContentHash_FileName";
        CREATE UNIQUE INDEX "IX_rag_staging_ContentHash_SourceKey"
            ON rag_staging ("ContentHash", "SourceKey") WHERE "SourceKey" <> '';
        CREATE UNIQUE INDEX "IX_rag_staging_ContentHash_FileName" ON rag_staging ("ContentHash", "FileName")
            WHERE "SourceKey" = '';
        CREATE INDEX ix_rag_chunk_claim_extraction ON rag_chunk ("CreatedAt", "Id")
            WHERE "ClaimsExtractedAt" IS NULL;
        CREATE INDEX ix_rag_chunk_embedding_backfill ON rag_chunk ("CreatedAt", "Id")
            WHERE "EmbeddedAt" IS NULL;
        CREATE INDEX ix_rag_claim_review ON rag_claim ("Status", "CreatedAt", "Id");
        CREATE INDEX ix_rag_claim_search ON rag_claim USING GIN
            (to_tsvector('english', "Subject" || ' ' || coalesce("Predicate", '') || ' ' || coalesce("Object", '')))
            WHERE "Status" = 'Approved';
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP INDEX "IX_rag_staging_ContentHash_SourceKey", "IX_rag_staging_ContentHash_FileName";
        ALTER TABLE rag_staging DROP COLUMN "SourceKey";
        CREATE UNIQUE INDEX "IX_rag_staging_ContentHash_FileName" ON rag_staging ("ContentHash", "FileName");
        DROP INDEX ix_rag_claim_search, ix_rag_claim_review, ix_rag_chunk_claim_extraction,
            ix_rag_chunk_embedding_backfill;
        ALTER TABLE rag_source DROP COLUMN "ClaimAutoApprovalMinimumConfidence";
        ALTER TABLE rag_claim DROP COLUMN "ReviewedAt", DROP COLUMN "ReviewedBy", DROP COLUMN "Evidence";
        ALTER TABLE rag_chunk DROP COLUMN "EmbeddingLeaseId", DROP COLUMN "EmbeddingLeaseUntil",
                             DROP COLUMN "ClaimExtractionLeaseId", DROP COLUMN "ClaimExtractionLeaseUntil",
                             DROP COLUMN "ClaimsExtractedAt";
        """);
}
