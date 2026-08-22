namespace Example.QueueSystem.Infrastructure.Entities;

/// <summary>
/// An append-only log of every ticket handed out, newest last.
/// </summary>
public class QueueTicketEntity
{
    public long Id { get; set; }

    public string TicketNumber { get; set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; set; }
}
