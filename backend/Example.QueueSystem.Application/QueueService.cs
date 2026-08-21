namespace Example.QueueSystem.Application;

public class QueueService : IQueueService
{
    private readonly IQueueRepository _queueRepository;

    public QueueService(IQueueRepository queueRepository)
    {
        _queueRepository = queueRepository;
    }

    public Task<TakeTicketResult> TakeTicketAsync(CancellationToken ct) =>
        _queueRepository.TakeTicketAsync(ct);

    public Task ClearAsync(CancellationToken ct) =>
        _queueRepository.ClearAsync(ct);

    public Task<CurrentQueueState> GetCurrentAsync(CancellationToken ct) =>
        _queueRepository.GetCurrentAsync(ct);
}
