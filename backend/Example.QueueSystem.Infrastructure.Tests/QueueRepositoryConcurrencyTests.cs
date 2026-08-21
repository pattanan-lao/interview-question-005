using System.Collections.Concurrent;
using Example.QueueSystem.Infrastructure;

namespace Example.QueueSystem.Infrastructure.Tests;

public class QueueRepositoryConcurrencyTests : IAsyncLifetime
{
    private const string ConnectionStringEnvVar = "QUEUE_TEST_DB_CONNECTION_STRING";
    private QueueRepository _repository = null!;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar)
            ?? throw new InvalidOperationException(
                $"Set the {ConnectionStringEnvVar} environment variable to a real PostgreSQL " +
                "connection string (pointing at a disposable test database) to run this test. " +
                "See README.md 'Running the integration tests'.");

        _repository = new QueueRepository(connectionString);
        await QueueSchema.ResetAsync(connectionString);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TakeTicketAsync_UnderConcurrentLoad_IssuesNoDuplicateTickets()
    {
        const int concurrentRequests = 50;
        var tickets = new ConcurrentBag<string>();

        var tasks = Enumerable.Range(0, concurrentRequests).Select(async _ =>
        {
            var result = await _repository.TakeTicketAsync(CancellationToken.None);
            Assert.True(result.Success);
            tickets.Add(result.TicketNumber!);
        });

        await Task.WhenAll(tasks);

        Assert.Equal(concurrentRequests, tickets.Count);
        Assert.Equal(concurrentRequests, tickets.Distinct().Count());
    }

    [Fact]
    public async Task TakeTicketAsync_AfterClear_RestartsAtA0()
    {
        await _repository.TakeTicketAsync(CancellationToken.None);
        await _repository.ClearAsync(CancellationToken.None);

        var result = await _repository.TakeTicketAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("A0", result.TicketNumber);
    }

    [Fact]
    public async Task TakeTicketAsync_WhenQueueIsFull_ReturnsFailureWithoutThrowing()
    {
        const int totalCapacity = 26 * 10; // A0-Z9

        for (var i = 0; i < totalCapacity; i++)
        {
            var result = await _repository.TakeTicketAsync(CancellationToken.None);
            Assert.True(result.Success);
        }

        var exhaustedResult = await _repository.TakeTicketAsync(CancellationToken.None);

        Assert.False(exhaustedResult.Success);
        Assert.Null(exhaustedResult.TicketNumber);
        Assert.Null(exhaustedResult.IssuedAt);
    }

    [Fact]
    public async Task GetCurrentAsync_WhenQueueIsFresh_ReturnsZeroZeroWithNoIssuedAt()
    {
        var current = await _repository.GetCurrentAsync(CancellationToken.None);

        Assert.Equal("00", current.TicketNumber);
        Assert.Null(current.IssuedAt);
    }

    [Fact]
    public async Task GetCurrentAsync_AfterTakingATicket_ReturnsThatTicketWithIssuedAt()
    {
        var taken = await _repository.TakeTicketAsync(CancellationToken.None);
        Assert.True(taken.Success);

        var current = await _repository.GetCurrentAsync(CancellationToken.None);

        Assert.Equal(taken.TicketNumber, current.TicketNumber);
        Assert.NotNull(current.IssuedAt);
    }
}
