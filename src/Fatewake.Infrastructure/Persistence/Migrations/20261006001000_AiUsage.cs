using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Fatewake.Infrastructure.Persistence.Migrations;
/// <summary>Adds AI attempt accounting without rewriting historical image generation.</summary>
[DbContext(typeof(FatewakeDbContext))]
[Migration("20261006001000_AiUsage")]
public sealed class AiUsage : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder m) => m.Sql("""
CREATE TABLE ai_usage (
 "Id" uuid PRIMARY KEY, "Provider" text NOT NULL, "Model" text NOT NULL, "Operation" text NOT NULL,
 "AttemptKey" text NOT NULL, "WorkJobId" uuid NULL, "AccountId" uuid NULL, "SurvivorId" uuid NULL,
 "StartedAt" timestamptz NOT NULL, "CompletedAt" timestamptz NULL, "Outcome" text NOT NULL,
 "ProviderRequestId" text NULL, "AccountingKey" text NULL, "DuplicateOfId" uuid NULL,
 "InputTokens" bigint NULL, "OutputTokens" bigint NULL, "TotalTokens" bigint NULL, "CachedTokens" bigint NULL,
 "TextInputTokens" bigint NULL, "ImageInputTokens" bigint NULL, "ImageOutputTokens" bigint NULL,
 "Credits" numeric NULL, "ComputeSeconds" numeric NULL, "Cost" numeric NULL, "Currency" text NULL
);
CREATE UNIQUE INDEX "IX_ai_usage_Provider_AttemptKey" ON ai_usage ("Provider","AttemptKey");
CREATE UNIQUE INDEX "IX_ai_usage_Provider_AccountingKey" ON ai_usage ("Provider","AccountingKey") WHERE "AccountingKey" IS NOT NULL;
CREATE INDEX "IX_ai_usage_StartedAt" ON ai_usage ("StartedAt");
CREATE INDEX "IX_ai_usage_Provider_Model_Operation_StartedAt" ON ai_usage ("Provider","Model","Operation","StartedAt");
CREATE INDEX "IX_ai_usage_WorkJobId" ON ai_usage ("WorkJobId");
CREATE TABLE ai_usage_tracking ("Id" integer PRIMARY KEY, "StartedAt" timestamptz NOT NULL);
INSERT INTO ai_usage_tracking VALUES (1, NOW());
CREATE FUNCTION ai_calendar_key(value timestamptz, zone text, pattern text) RETURNS text LANGUAGE sql STABLE AS $$ SELECT to_char(value AT TIME ZONE zone, pattern) $$;
CREATE FUNCTION ai_json_text(value jsonb, field text) RETURNS text LANGUAGE sql IMMUTABLE AS $$ SELECT value ->> field $$;
""");
    /// <inheritdoc />
    protected override void Down(MigrationBuilder m) => m.Sql("DROP TABLE ai_usage; DROP TABLE ai_usage_tracking; DROP FUNCTION ai_calendar_key; DROP FUNCTION ai_json_text;");
}
