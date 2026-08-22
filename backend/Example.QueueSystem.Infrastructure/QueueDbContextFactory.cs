using Microsoft.EntityFrameworkCore;

namespace Example.QueueSystem.Infrastructure;

/// <summary>
/// Builds <see cref="QueueDbContext"/> instances from a bare connection string, for callers
/// that have no dependency-injection container — chiefly the integration tests. Inside the API
/// the equivalent factory comes from <c>AddQueueInfrastructure</c>.
/// </summary>
public sealed class QueueDbContextFactory : IDbContextFactory<QueueDbContext>
{
    private readonly DbContextOptions<QueueDbContext> _options;

    public QueueDbContextFactory(string connectionString)
    {
        _options = new DbContextOptionsBuilder<QueueDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }

    public QueueDbContext CreateDbContext() => new(_options);
}
