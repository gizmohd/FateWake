using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Fatewake.Infrastructure.Persistence.Migrations;

[DbContext(typeof(FatewakeDbContext))]
[Migration("20261006000200_StatusEnums")]
public sealed class StatusEnums : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
ALTER TABLE account ALTER COLUMN "Status" TYPE integer USING CASE "Status" WHEN 'active' THEN 1 END;
ALTER TABLE survivor ALTER COLUMN "Status" TYPE integer USING CASE "Status" WHEN 'active' THEN 1 END;
ALTER TABLE event_instance ALTER COLUMN "Status" TYPE integer USING CASE "Status" WHEN 'active' THEN 1 END;
ALTER TABLE wake ALTER COLUMN "State" TYPE integer USING CASE "State" WHEN 'active' THEN 1 END;
CREATE INDEX "IX_event_instance_SurvivorId_Status_SurvivorDay" ON event_instance ("SurvivorId", "Status", "SurvivorDay");
""");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("""
DROP INDEX "IX_event_instance_SurvivorId_Status_SurvivorDay";
ALTER TABLE account ALTER COLUMN "Status" TYPE text USING CASE "Status" WHEN 1 THEN 'active' END;
ALTER TABLE survivor ALTER COLUMN "Status" TYPE text USING CASE "Status" WHEN 1 THEN 'active' END;
ALTER TABLE event_instance ALTER COLUMN "Status" TYPE text USING CASE "Status" WHEN 1 THEN 'active' END;
ALTER TABLE wake ALTER COLUMN "State" TYPE text USING CASE "State" WHEN 1 THEN 'active' END;
""");
    }
}
