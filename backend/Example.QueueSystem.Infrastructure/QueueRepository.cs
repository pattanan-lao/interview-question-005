using Example.QueueSystem.Application;
using Example.QueueSystem.Domain;
using Example.QueueSystem.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Example.QueueSystem.Infrastructure;

public class QueueRepository : IQueueRepository
{
    /// <summary>
    /// Upper bound on optimistic-concurrency retries for a single ticket request. Each round
    /// of contention lets at least one caller through, so this only needs to exceed the number
    /// of writers realistically racing for a ticket at the same instant.
    /// </summary>
    public const int DefaultMaxAttempts = 100;

    private readonly IDbContextFactory<QueueDbContext> _dbContextFactory;
    private readonly int _maxAttempts;

    public QueueRepository(IDbContextFactory<QueueDbContext> dbContextFactory, int maxAttempts = DefaultMaxAttempts)
    {
        _dbContextFactory = dbContextFactory;
        _maxAttempts = maxAttempts;
    }

    public async Task<TakeTicketResult> TakeTicketAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < _maxAttempts; attempt++)
        {
            // A fresh context per attempt: once SaveChanges throws a concurrency exception its
            // change tracker holds stale original values, so re-reading into the same context
            // would need an explicit reload anyway.
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var state = await db.QueueStates
                .SingleAsync(s => s.Id == QueueStateEntity.SingletonId, ct);

            var current = ToPosition(state.CurrentLetterIndex, state.CurrentDigit);
            var next = TicketNumbering.Next(current);
            if (next.Outcome == NextTicketOutcome.Exhausted)
            {
                return new TakeTicketResult(false, null, null);
            }

            var position = next.Position!.Value;
            var issuedAt = DateTimeOffset.UtcNow;
            var ticketNumber = TicketNumbering.Format(position);

            state.CurrentLetterIndex = (short)position.LetterIndex;
            state.CurrentDigit = (short)position.Digit;
            state.UpdatedAt = issuedAt;

            // The token is application-managed: we write the next value, while EF Core matches
            // on the value we read. Whoever commits second finds no row to update.
            state.Version++;

            db.QueueTickets.Add(new QueueTicketEntity
            {
                TicketNumber = ticketNumber,
                IssuedAt = issuedAt,
            });

            try
            {
                // One SaveChanges, so EF Core wraps the state update and the ticket insert in a
                // single transaction: a lost concurrency race rolls back both, never leaving a
                // ticket row behind for a number the queue did not actually advance to.
                await db.SaveChangesAsync(ct);
                return new TakeTicketResult(true, ticketNumber, issuedAt);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another request advanced the queue between our read and our write. Back off
                // briefly so 50 racing callers do not all retry in lockstep, then re-read.
                await Task.Delay(Random.Shared.Next(1, 15), ct);
            }
        }

        throw new InvalidOperationException(
            $"Could not issue a ticket after {_maxAttempts} optimistic-concurrency attempts. " +
            "The queue is under heavier write contention than the retry budget allows.");
    }

    public async Task ClearAsync(CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        // ExecuteUpdate issues a single UPDATE without loading the row, which deliberately
        // sidesteps the concurrency check: clearing is an operator override that should win
        // over any ticket issued a moment earlier, not lose a race to it. It still bumps the
        // token, so an in-flight TakeTicket that read the pre-clear position loses its race
        // and retries against the cleared queue instead of resurrecting the old number.
        await db.QueueStates
            .Where(s => s.Id == QueueStateEntity.SingletonId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.CurrentLetterIndex, (short?)null)
                    .SetProperty(s => s.CurrentDigit, (short?)null)
                    .SetProperty(s => s.UpdatedAt, DateTimeOffset.UtcNow)
                    .SetProperty(s => s.Version, s => s.Version + 1),
                ct);
    }

    public async Task<CurrentQueueState> GetCurrentAsync(CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var state = await db.QueueStates
            .AsNoTracking()
            .Where(s => s.Id == QueueStateEntity.SingletonId)
            .Select(s => new { s.CurrentLetterIndex, s.CurrentDigit })
            .SingleAsync(ct);

        var current = ToPosition(state.CurrentLetterIndex, state.CurrentDigit);

        // Only meaningful when a ticket is outstanding: after a clear the queue reads "00" and
        // the last ticket's timestamp would be misleading, so it is not queried at all.
        DateTimeOffset? issuedAt = current is null
            ? null
            : await db.QueueTickets
                .AsNoTracking()
                .OrderByDescending(t => t.Id)
                .Select(t => (DateTimeOffset?)t.IssuedAt)
                .FirstOrDefaultAsync(ct);

        return new CurrentQueueState(TicketNumbering.Format(current), issuedAt);
    }

    private static QueuePosition? ToPosition(short? letterIndex, short? digit) =>
        letterIndex is null || digit is null
            ? null
            : new QueuePosition(letterIndex.Value, digit.Value);
}
