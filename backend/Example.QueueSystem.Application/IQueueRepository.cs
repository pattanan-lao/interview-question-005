namespace Example.QueueSystem.Application;

public record TakeTicketResult(bool Success, string? TicketNumber, DateTimeOffset? IssuedAt);

public record CurrentQueueState(string TicketNumber, DateTimeOffset? IssuedAt);

public interface IQueueRepository
{
    Task<TakeTicketResult> TakeTicketAsync(CancellationToken ct);

    Task ClearAsync(CancellationToken ct);

    Task<CurrentQueueState> GetCurrentAsync(CancellationToken ct);
}
