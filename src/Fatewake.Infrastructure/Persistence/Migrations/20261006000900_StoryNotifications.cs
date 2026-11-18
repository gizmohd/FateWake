using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Fatewake.Infrastructure.Persistence.Migrations;
/// <summary>Adds durable notifications for important audience-specific story changes and revelations.</summary>
[DbContext(typeof(FatewakeDbContext))]
[Migration("20261006000900_StoryNotifications")]
public sealed class StoryNotifications : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder m) => m.Sql("""
ALTER TABLE story_panel ADD COLUMN "NotificationRevision" integer NOT NULL DEFAULT 0;
ALTER TABLE story_panel ADD COLUMN "SeenRevision" integer NOT NULL DEFAULT 0;
""");
    /// <inheritdoc />
    protected override void Down(MigrationBuilder m) => m.Sql("""
ALTER TABLE story_panel DROP COLUMN "NotificationRevision";
ALTER TABLE story_panel DROP COLUMN "SeenRevision";
""");
}
