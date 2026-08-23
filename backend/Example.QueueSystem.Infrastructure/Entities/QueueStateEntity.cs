namespace Example.QueueSystem.Infrastructure.Entities;

/// <summary>
/// The single row (Id == <see cref="SingletonId"/>) that holds the queue's current position.
/// A null <see cref="CurrentLetterIndex"/> means no ticket has been issued since the last clear.
/// </summary>
public class QueueStateEntity
{
    public const short SingletonId = 1;

    public short Id { get; set; }

    public short? CurrentLetterIndex { get; set; }

    public short? CurrentDigit { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token. Every writer increments it, and EF Core puts the value it
    /// read into the UPDATE's WHERE clause, so a row another request already advanced matches
    /// zero rows and raises
    /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/> rather than
    /// being silently overwritten.
    /// </summary>
    /// <remarks>
    /// An ordinary column rather than PostgreSQL's system <c>xmin</c>: mapping <c>xmin</c> makes
    /// EF Core's migration scaffolder emit a real <c>xmin</c> column, which PostgreSQL refuses
    /// to create because the name collides with the system column.
    /// </remarks>
    public int Version { get; set; }
}
