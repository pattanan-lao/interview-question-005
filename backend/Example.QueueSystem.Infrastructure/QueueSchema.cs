using Npgsql;

namespace Example.QueueSystem.Infrastructure;

public static class QueueSchema
{
    private const string CreateTablesSql = """
        CREATE TABLE IF NOT EXISTS queue_state (
            id SMALLINT PRIMARY KEY,
            current_letter_index SMALLINT NULL,
            current_digit SMALLINT NULL,
            updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        INSERT INTO queue_state (id, current_letter_index, current_digit, updated_at)
        VALUES (1, NULL, NULL, now())
        ON CONFLICT (id) DO NOTHING;

        CREATE TABLE IF NOT EXISTS queue_tickets (
            id BIGSERIAL PRIMARY KEY,
            ticket_number TEXT NOT NULL,
            issued_at TIMESTAMPTZ NOT NULL
        );
        """;

    private const string DropTablesSql = """
        DROP TABLE IF EXISTS queue_tickets;
        DROP TABLE IF EXISTS queue_state;
        """;

    public static async Task EnsureCreatedAsync(string connectionString, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(CreateTablesSql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    public static async Task ResetAsync(string connectionString, CancellationToken ct = default)
    {
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand(DropTablesSql, connection);
            await command.ExecuteNonQueryAsync(ct);
        }

        await EnsureCreatedAsync(connectionString, ct);
    }
}
