using Microsoft.EntityFrameworkCore;

namespace Example.QueueSystem.Infrastructure;

public static class QueueDbInitializer
{
    /// <summary>
    /// Applies any pending EF Core migrations, creating the database if it does not exist.
    /// The singleton <c>queue_state</c> row is seeded by the initial migration, so a freshly
    /// migrated database is immediately usable.
    /// </summary>
    public static async Task MigrateAsync(
        IDbContextFactory<QueueDbContext> dbContextFactory,
        CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
    }

    /// <summary>
    /// Drops the database and rebuilds it from the migrations. Intended for integration tests
    /// pointed at a disposable database — never call it against one holding real tickets.
    /// </summary>
    public static async Task ResetAsync(
        IDbContextFactory<QueueDbContext> dbContextFactory,
        CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        await db.Database.EnsureDeletedAsync(ct);
        await db.Database.MigrateAsync(ct);
    }
}
