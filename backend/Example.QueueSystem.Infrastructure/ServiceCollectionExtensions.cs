using Example.QueueSystem.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Example.QueueSystem.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EF Core context factory and the repository that sits on it.
    /// </summary>
    /// <remarks>
    /// A factory rather than a scoped <c>DbContext</c>: <see cref="QueueRepository"/> retries a
    /// lost optimistic-concurrency race on a fresh context, which a per-request context could
    /// not provide. Pooling keeps that cheap by reusing the underlying context instances.
    /// </remarks>
    public static IServiceCollection AddQueueInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddPooledDbContextFactory<QueueDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<IQueueRepository>(sp =>
            new QueueRepository(sp.GetRequiredService<IDbContextFactory<QueueDbContext>>()));

        return services;
    }
}
