namespace Example.QueueSystem.Application;

public interface IQueueService
{
    Task<TakeTicketResult> TakeTicketAsync(CancellationToken ct);

    Task ClearAsync(CancellationToken ct);

    Task<CurrentQueueState> GetCurrentAsync(CancellationToken ct);
}
