using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Example.QueueSystem.Infrastructure;

/// <summary>
/// Used only by the <c>dotnet ef</c> tooling. Scaffolding a migration compares models and never
/// opens a connection, so the placeholder connection string is enough; set
/// <c>QUEUE_DB_CONNECTION_STRING</c> when running a command that does touch the database
/// (for example <c>dotnet ef database update</c>).
/// </summary>
public sealed class DesignTimeQueueDbContextFactory : IDesignTimeDbContextFactory<QueueDbContext>
{
    private const string ConnectionStringEnvVar = "QUEUE_DB_CONNECTION_STRING";

    private const string PlaceholderConnectionString =
        "Host=localhost;Database=queue_system;Username=queue_app;Password=design-time";

    public QueueDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = PlaceholderConnectionString;
        }

        var options = new DbContextOptionsBuilder<QueueDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new QueueDbContext(options);
    }
}
