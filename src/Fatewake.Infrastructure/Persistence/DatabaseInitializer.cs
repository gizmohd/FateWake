using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(FatewakeDbContext db, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        if (await db.Realms.AnyAsync(ct)) return;
        db.Realms.Add(new RealmRecord { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Key = "the-silence", Name = "The Silence" });
        await db.SaveChangesAsync(ct);
    }
}
