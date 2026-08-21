using Example.QueueSystem.Application;
using Example.QueueSystem.Domain;
using Npgsql;

namespace Example.QueueSystem.Infrastructure;

public class QueueRepository : IQueueRepository
{
    private readonly string _connectionString;

    public QueueRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<TakeTicketResult> TakeTicketAsync(CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        QueuePosition? current = null;
        await using (var selectCommand = new NpgsqlCommand(
            "SELECT current_letter_index, current_digit FROM queue_state WHERE id = 1 FOR UPDATE",
            connection, transaction))
        {
            await using var reader = await selectCommand.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            if (!reader.IsDBNull(0))
            {
                current = new QueuePosition(reader.GetInt16(0), reader.GetInt16(1));
            }
        }

        var next = TicketNumbering.Next(current);
        if (next.Outcome == NextTicketOutcome.Exhausted)
        {
            await transaction.RollbackAsync(ct);
            return new TakeTicketResult(false, null, null);
        }

        var position = next.Position!.Value;
        var issuedAt = DateTimeOffset.UtcNow;
        var ticketNumber = TicketNumbering.Format(position);

        await using (var updateCommand = new NpgsqlCommand(
            "UPDATE queue_state SET current_letter_index = $1, current_digit = $2, updated_at = $3 WHERE id = 1",
            connection, transaction))
        {
            updateCommand.Parameters.Add(new NpgsqlParameter { Value = (short)position.LetterIndex });
            updateCommand.Parameters.Add(new NpgsqlParameter { Value = (short)position.Digit });
            updateCommand.Parameters.Add(new NpgsqlParameter { Value = issuedAt });
            await updateCommand.ExecuteNonQueryAsync(ct);
        }

        await using (var insertCommand = new NpgsqlCommand(
            "INSERT INTO queue_tickets (ticket_number, issued_at) VALUES ($1, $2)",
            connection, transaction))
        {
            insertCommand.Parameters.Add(new NpgsqlParameter { Value = ticketNumber });
            insertCommand.Parameters.Add(new NpgsqlParameter { Value = issuedAt });
            await insertCommand.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return new TakeTicketResult(true, ticketNumber, issuedAt);
    }

    public async Task ClearAsync(CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "UPDATE queue_state SET current_letter_index = NULL, current_digit = NULL, updated_at = $1 WHERE id = 1",
            connection);
        command.Parameters.Add(new NpgsqlParameter { Value = DateTimeOffset.UtcNow });
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<CurrentQueueState> GetCurrentAsync(CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        QueuePosition? current = null;
        await using (var command = new NpgsqlCommand(
            "SELECT current_letter_index, current_digit FROM queue_state WHERE id = 1", connection))
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            if (!reader.IsDBNull(0))
            {
                current = new QueuePosition(reader.GetInt16(0), reader.GetInt16(1));
            }
        }

        var ticketNumber = TicketNumbering.Format(current);
        DateTimeOffset? issuedAt = null;

        if (current is not null)
        {
            await using var lastCommand = new NpgsqlCommand(
                "SELECT issued_at FROM queue_tickets ORDER BY id DESC LIMIT 1", connection);
            var result = await lastCommand.ExecuteScalarAsync(ct);
            if (result is DateTimeOffset dt)
            {
                issuedAt = dt;
            }
        }

        return new CurrentQueueState(ticketNumber, issuedAt);
    }
}
