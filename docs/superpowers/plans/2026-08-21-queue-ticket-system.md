# Queue Ticket System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the 3-screen queue-ticket kiosk (IT 05-1/2/3) from interview test No. 5: an ASP.NET Core Web API backend backed by PostgreSQL with concurrency-safe ticket issuing, and an Angular frontend implementing the three mockup screens.

**Architecture:** Monorepo with `/backend` (ASP.NET Core Web API, `net10.0`) and `/frontend` (Angular 22, standalone components). The backend owns all queue state in PostgreSQL and exposes 3 REST endpoints; ticket issuance is made concurrency-safe with a `SELECT ... FOR UPDATE` row lock inside a single transaction. The frontend is a thin client: 3 routed pages that call the API and navigate between each other exactly as the mockups specify.

**Tech Stack:** ASP.NET Core Web API (net10.0, C# with controllers), Npgsql 10.0.3 (raw ADO.NET, no ORM), PostgreSQL 18, xUnit 2.9.3, Angular 22 (standalone components, `provideHttpClient`), npm.

**Spec:** `docs/superpowers/specs/2026-08-21-queue-ticket-system-design.md`

## Global Constraints

- Solution name: `Example.QueueSystem.sln`; backend root namespace: `Example.QueueSystem.Api` (per spec "Naming").
- Frontend package name: `example-com-queue-frontend`; browser title: "Example.com Queue System" (per spec "Naming").
- Ticket sequence: `A0` → `A1` … `A9` → `B0` … `Z9`. After `Z9`, further "take ticket" requests are rejected (HTTP 409) — no wraparound (per spec "Ticket numbering rules").
- "Clear Queue" resets state to `NULL`/`NULL`, displayed as `"00"` (per spec "Data model").
- Ticket issuance MUST run inside a single PostgreSQL transaction using `SELECT ... FOR UPDATE` on the `queue_state` row (per spec "Concurrency safety") — this is the requirement doc's "must prevent simultaneous ticket-taking."
- No Docker for local dev; PostgreSQL is assumed already running locally (per spec "Stack").
- Backend targets `net10.0` with default C# language version (per spec "Stack", matching installed SDK 10.0.400).
- Do not commit real database credentials. `appsettings.Development.json` (which holds the real local connection string) is gitignored; a `.example` version with a placeholder password is committed instead.

**Local environment already verified/prepared during planning (do not redo):**
- .NET SDK 10.0.400, Node.js v26.7.0, npm 11.19.0, PostgreSQL 18 (service `postgresql-x64-18`, running, port 5432) are all installed and working.
- A dedicated PostgreSQL role `queue_app` and two databases owned by it already exist: `queue_system` (dev) and `queue_system_test` (integration tests). The generated password for `queue_app` is stored only in the executor's local environment/notes — never write it into any file under `docs/superpowers/` or any other committed path. Use it only inside the gitignored `appsettings.Development.json` and as the local value of the `QUEUE_TEST_DB_CONNECTION_STRING` environment variable when running integration tests.

---

### Task 1: Scaffold Backend Solution & Projects

**Files:**
- Create: `backend/Example.QueueSystem.sln`
- Create: `backend/Example.QueueSystem.Api/` (via `dotnet new webapi`)
- Create: `backend/Example.QueueSystem.Api.Tests/` (via `dotnet new xunit`)
- Modify: `backend/Example.QueueSystem.Api/Properties/launchSettings.json`
- Create: `.gitignore` (repo root)

**Interfaces:**
- Produces: a buildable solution `backend/Example.QueueSystem.sln` containing both projects, with the test project referencing the API project. Backend listens on `http://localhost:5080` in the `Development` environment.

- [ ] **Step 1: Create the solution and projects**

Run from the repo root (`C:\Workspace\interview-question-005`):

```bash
dotnet new sln -n Example.QueueSystem -o backend
dotnet new webapi -n Example.QueueSystem.Api --use-controllers -o backend/Example.QueueSystem.Api
dotnet new xunit -n Example.QueueSystem.Api.Tests -o backend/Example.QueueSystem.Api.Tests
dotnet sln backend/Example.QueueSystem.sln add backend/Example.QueueSystem.Api/Example.QueueSystem.Api.csproj
dotnet sln backend/Example.QueueSystem.sln add backend/Example.QueueSystem.Api.Tests/Example.QueueSystem.Api.Tests.csproj
dotnet add backend/Example.QueueSystem.Api.Tests/Example.QueueSystem.Api.Tests.csproj reference backend/Example.QueueSystem.Api/Example.QueueSystem.Api.csproj
dotnet add backend/Example.QueueSystem.Api/Example.QueueSystem.Api.csproj package Npgsql --version 10.0.3
```

- [ ] **Step 2: Remove template sample files if present**

The `webapi` template may generate `backend/Example.QueueSystem.Api/WeatherForecast.cs` and `backend/Example.QueueSystem.Api/Controllers/WeatherForecastController.cs`. If either exists, delete it — they are not part of this app.

- [ ] **Step 3: Pin the dev server to a fixed HTTP-only port**

Overwrite `backend/Example.QueueSystem.Api/Properties/launchSettings.json` with:

```json
{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5080",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

(This drops the generated `https`/`IIS Express` profiles. Plain HTTP avoids requiring `dotnet dev-certs https --trust` for whoever reviews this — acceptable for a local interview-test kiosk.)

- [ ] **Step 4: Create the root `.gitignore`**

Create `.gitignore` at the repo root:

```gitignore
# .NET
backend/**/bin/
backend/**/obj/
backend/Example.QueueSystem.Api/appsettings.Development.json

# Angular / Node
frontend/node_modules/
frontend/dist/
frontend/.angular/

# OS
.DS_Store
Thumbs.db
```

- [ ] **Step 5: Verify the solution builds**

Run: `dotnet build backend/Example.QueueSystem.sln`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 6: Commit**

```bash
git add backend .gitignore
git commit -m "Scaffold backend solution: Web API + xUnit test project"
```

---

### Task 2: Ticket Numbering Domain Logic

**Files:**
- Create: `backend/Example.QueueSystem.Api/Services/TicketNumbering.cs`
- Test: `backend/Example.QueueSystem.Api.Tests/TicketNumberingTests.cs`

**Interfaces:**
- Consumes: nothing (pure logic, no DB).
- Produces (used by Task 3):
  - `readonly record struct QueuePosition(int LetterIndex, int Digit)`
  - `enum NextTicketOutcome { Issued, Exhausted }`
  - `readonly record struct NextTicketResult(NextTicketOutcome Outcome, QueuePosition? Position)`
  - `static class TicketNumbering` with `static NextTicketResult Next(QueuePosition? current)` and `static string Format(QueuePosition? position)`, all in namespace `Example.QueueSystem.Api.Services`.

- [ ] **Step 1: Write the failing tests**

Create `backend/Example.QueueSystem.Api.Tests/TicketNumberingTests.cs`:

```csharp
using Example.QueueSystem.Api.Services;

namespace Example.QueueSystem.Api.Tests;

public class TicketNumberingTests
{
    [Fact]
    public void Next_WhenNoCurrentTicket_ReturnsA0()
    {
        var result = TicketNumbering.Next(null);

        Assert.Equal(NextTicketOutcome.Issued, result.Outcome);
        Assert.Equal(new QueuePosition(0, 0), result.Position);
        Assert.Equal("A0", TicketNumbering.Format(result.Position));
    }

    [Fact]
    public void Next_IncrementsDigitWithinSameLetter()
    {
        var current = new QueuePosition(0, 3); // A3

        var result = TicketNumbering.Next(current);

        Assert.Equal(NextTicketOutcome.Issued, result.Outcome);
        Assert.Equal("A4", TicketNumbering.Format(result.Position));
    }

    [Fact]
    public void Next_WhenDigitIsNine_RollsOverToNextLetter()
    {
        var current = new QueuePosition(0, 9); // A9

        var result = TicketNumbering.Next(current);

        Assert.Equal(NextTicketOutcome.Issued, result.Outcome);
        Assert.Equal("B0", TicketNumbering.Format(result.Position));
    }

    [Fact]
    public void Next_WhenAtZ9_ReturnsExhausted()
    {
        var current = new QueuePosition(25, 9); // Z9

        var result = TicketNumbering.Next(current);

        Assert.Equal(NextTicketOutcome.Exhausted, result.Outcome);
        Assert.Null(result.Position);
    }

    [Fact]
    public void Format_WhenPositionIsNull_ReturnsZeroZero()
    {
        Assert.Equal("00", TicketNumbering.Format(null));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/Example.QueueSystem.Api.Tests/Example.QueueSystem.Api.Tests.csproj`
Expected: build FAILS (`TicketNumbering` does not exist in namespace `Example.QueueSystem.Api.Services`).

- [ ] **Step 3: Implement `TicketNumbering`**

Create `backend/Example.QueueSystem.Api/Services/TicketNumbering.cs`:

```csharp
namespace Example.QueueSystem.Api.Services;

public readonly record struct QueuePosition(int LetterIndex, int Digit);

public enum NextTicketOutcome
{
    Issued,
    Exhausted,
}

public readonly record struct NextTicketResult(NextTicketOutcome Outcome, QueuePosition? Position);

public static class TicketNumbering
{
    public const int MinLetterIndex = 0;
    public const int MaxLetterIndex = 25; // 'Z'
    public const int MinDigit = 0;
    public const int MaxDigit = 9;

    public static NextTicketResult Next(QueuePosition? current)
    {
        if (current is null)
        {
            return new NextTicketResult(NextTicketOutcome.Issued, new QueuePosition(MinLetterIndex, MinDigit));
        }

        var position = current.Value;

        if (position.Digit < MaxDigit)
        {
            return new NextTicketResult(NextTicketOutcome.Issued, position with { Digit = position.Digit + 1 });
        }

        if (position.LetterIndex < MaxLetterIndex)
        {
            return new NextTicketResult(NextTicketOutcome.Issued, new QueuePosition(position.LetterIndex + 1, MinDigit));
        }

        return new NextTicketResult(NextTicketOutcome.Exhausted, null);
    }

    public static string Format(QueuePosition? position)
    {
        if (position is null)
        {
            return "00";
        }

        var letter = (char)('A' + position.Value.LetterIndex);
        return $"{letter}{position.Value.Digit}";
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/Example.QueueSystem.Api.Tests/Example.QueueSystem.Api.Tests.csproj`
Expected: `Passed! - Failed: 0, Passed: 5`

- [ ] **Step 5: Commit**

```bash
git add backend/Example.QueueSystem.Api/Services/TicketNumbering.cs backend/Example.QueueSystem.Api.Tests/TicketNumberingTests.cs
git commit -m "Add ticket numbering domain logic with unit tests"
```

---

### Task 3: Queue Repository, Database Schema & Concurrency Test

**Files:**
- Create: `backend/Example.QueueSystem.Api/Data/QueueSchema.cs`
- Create: `backend/Example.QueueSystem.Api/Services/IQueueRepository.cs`
- Create: `backend/Example.QueueSystem.Api/Services/QueueRepository.cs`
- Test: `backend/Example.QueueSystem.Api.Tests/QueueRepositoryConcurrencyTests.cs`

**Interfaces:**
- Consumes: `TicketNumbering.Next`, `TicketNumbering.Format`, `QueuePosition`, `NextTicketOutcome` from Task 2.
- Produces (used by Task 4):
  - `static class QueueSchema` with `static Task EnsureCreatedAsync(string connectionString, CancellationToken ct = default)` and `static Task ResetAsync(string connectionString, CancellationToken ct = default)`, namespace `Example.QueueSystem.Api.Data`.
  - `record TakeTicketResult(bool Success, string? TicketNumber, DateTimeOffset? IssuedAt)`
  - `record CurrentQueueState(string TicketNumber, DateTimeOffset? IssuedAt)`
  - `interface IQueueRepository` with `Task<TakeTicketResult> TakeTicketAsync(CancellationToken ct)`, `Task ClearAsync(CancellationToken ct)`, `Task<CurrentQueueState> GetCurrentAsync(CancellationToken ct)`.
  - `class QueueRepository(string connectionString) : IQueueRepository`, namespace `Example.QueueSystem.Api.Services`.

- [ ] **Step 1: Implement the schema helper**

Create `backend/Example.QueueSystem.Api/Data/QueueSchema.cs`:

```csharp
using Npgsql;

namespace Example.QueueSystem.Api.Data;

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
```

- [ ] **Step 2: Define the repository contract**

Create `backend/Example.QueueSystem.Api/Services/IQueueRepository.cs`:

```csharp
namespace Example.QueueSystem.Api.Services;

public record TakeTicketResult(bool Success, string? TicketNumber, DateTimeOffset? IssuedAt);

public record CurrentQueueState(string TicketNumber, DateTimeOffset? IssuedAt);

public interface IQueueRepository
{
    Task<TakeTicketResult> TakeTicketAsync(CancellationToken ct);

    Task ClearAsync(CancellationToken ct);

    Task<CurrentQueueState> GetCurrentAsync(CancellationToken ct);
}
```

- [ ] **Step 3: Implement the concurrency-safe repository**

Create `backend/Example.QueueSystem.Api/Services/QueueRepository.cs`:

```csharp
using Npgsql;

namespace Example.QueueSystem.Api.Services;

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
```

- [ ] **Step 4: Write the concurrency integration test**

Create `backend/Example.QueueSystem.Api.Tests/QueueRepositoryConcurrencyTests.cs`:

```csharp
using System.Collections.Concurrent;
using Example.QueueSystem.Api.Data;
using Example.QueueSystem.Api.Services;

namespace Example.QueueSystem.Api.Tests;

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
}
```

- [ ] **Step 5: Run the integration tests against the local test database**

Set the connection string environment variable to the `queue_system_test` database prepared during planning (role `queue_app`, same password used for `appsettings.Development.json` in Task 4), then run the tests.

PowerShell:
```powershell
$env:QUEUE_TEST_DB_CONNECTION_STRING = "Host=localhost;Port=5432;Database=queue_system_test;Username=queue_app;Password=<local-queue_app-password>"
dotnet test backend/Example.QueueSystem.Api.Tests/Example.QueueSystem.Api.Tests.csproj
```

Expected: `Passed! - Failed: 0, Passed: 7` (5 from Task 2 + 2 here).

- [ ] **Step 6: Commit**

```bash
git add backend/Example.QueueSystem.Api/Data/QueueSchema.cs backend/Example.QueueSystem.Api/Services/IQueueRepository.cs backend/Example.QueueSystem.Api/Services/QueueRepository.cs backend/Example.QueueSystem.Api.Tests/QueueRepositoryConcurrencyTests.cs
git commit -m "Add concurrency-safe queue repository with row-locking and integration tests"
```

---

### Task 4: Queue API Controller, DTOs & Program.cs Wiring

**Files:**
- Create: `backend/Example.QueueSystem.Api/Dtos/TicketResponseDto.cs`
- Create: `backend/Example.QueueSystem.Api/Controllers/QueueController.cs`
- Modify: `backend/Example.QueueSystem.Api/Program.cs`
- Modify: `backend/Example.QueueSystem.Api/appsettings.json`
- Create: `backend/Example.QueueSystem.Api/appsettings.Development.json.example`
- Create: `backend/Example.QueueSystem.Api/appsettings.Development.json` (gitignored, not committed)

**Interfaces:**
- Consumes: `IQueueRepository`, `TakeTicketResult`, `CurrentQueueState` from Task 3.
- Produces (used by Task 6, the frontend `QueueApiService`): HTTP API —
  - `POST /api/queue/tickets` → 200 `{ "ticketNumber": string, "issuedAt": string|null }` or 409 `{ "message": string }`
  - `POST /api/queue/clear` → 200 `{ "ticketNumber": "00", "issuedAt": null }`
  - `GET /api/queue/current` → 200 `{ "ticketNumber": string, "issuedAt": string|null }`
  - CORS allows origin `http://localhost:4200`.

- [ ] **Step 1: Add the response DTO**

Create `backend/Example.QueueSystem.Api/Dtos/TicketResponseDto.cs`:

```csharp
namespace Example.QueueSystem.Api.Dtos;

public record TicketResponseDto(string TicketNumber, DateTimeOffset? IssuedAt);
```

- [ ] **Step 2: Add the controller**

Create `backend/Example.QueueSystem.Api/Controllers/QueueController.cs`:

```csharp
using Example.QueueSystem.Api.Dtos;
using Example.QueueSystem.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Example.QueueSystem.Api.Controllers;

[ApiController]
[Route("api/queue")]
public class QueueController : ControllerBase
{
    private readonly IQueueRepository _queueRepository;

    public QueueController(IQueueRepository queueRepository)
    {
        _queueRepository = queueRepository;
    }

    [HttpPost("tickets")]
    public async Task<ActionResult<TicketResponseDto>> TakeTicket(CancellationToken ct)
    {
        var result = await _queueRepository.TakeTicketAsync(ct);

        if (!result.Success)
        {
            return Conflict(new
            {
                message = "Queue exhausted: all tickets from A0 to Z9 have been issued. Clear the queue to continue.",
            });
        }

        return Ok(new TicketResponseDto(result.TicketNumber!, result.IssuedAt));
    }

    [HttpPost("clear")]
    public async Task<ActionResult<TicketResponseDto>> Clear(CancellationToken ct)
    {
        await _queueRepository.ClearAsync(ct);
        return Ok(new TicketResponseDto("00", null));
    }

    [HttpGet("current")]
    public async Task<ActionResult<TicketResponseDto>> GetCurrent(CancellationToken ct)
    {
        var state = await _queueRepository.GetCurrentAsync(ct);
        return Ok(new TicketResponseDto(state.TicketNumber, state.IssuedAt));
    }
}
```

- [ ] **Step 3: Wire up `Program.cs`**

Overwrite `backend/Example.QueueSystem.Api/Program.cs` with:

```csharp
using Example.QueueSystem.Api.Data;
using Example.QueueSystem.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("QueueDb")
    ?? throw new InvalidOperationException(
        "Connection string 'QueueDb' is not configured. Copy appsettings.Development.json.example " +
        "to appsettings.Development.json and fill in your local PostgreSQL password.");

builder.Services.AddSingleton<IQueueRepository>(_ => new QueueRepository(connectionString));
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDevClient", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

await QueueSchema.EnsureCreatedAsync(connectionString);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AngularDevClient");
app.UseAuthorization();
app.MapControllers();

app.Run();
```

- [ ] **Step 4: Add the (secret-free) connection string placeholder to `appsettings.json`**

Modify `backend/Example.QueueSystem.Api/appsettings.json` to add a `ConnectionStrings` section (keep existing `Logging`/`AllowedHosts` as generated):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "QueueDb": ""
  }
}
```

- [ ] **Step 5: Create the committed example dev-config**

Create `backend/Example.QueueSystem.Api/appsettings.Development.json.example`:

```json
{
  "ConnectionStrings": {
    "QueueDb": "Host=localhost;Port=5432;Database=queue_system;Username=queue_app;Password=REPLACE_ME"
  }
}
```

- [ ] **Step 6: Create the real (gitignored) local dev-config**

Create `backend/Example.QueueSystem.Api/appsettings.Development.json` (this file is covered by the root `.gitignore` from Task 1 — verify with `git status` that it does NOT show as a new/tracked file) using the real local `queue_app` password prepared during planning:

```json
{
  "ConnectionStrings": {
    "QueueDb": "Host=localhost;Port=5432;Database=queue_system;Username=queue_app;Password=<local-queue_app-password>"
  }
}
```

- [ ] **Step 7: Run the app and smoke-test all 3 endpoints**

Run: `dotnet run --project backend/Example.QueueSystem.Api`
Expected: log line `Now listening on: http://localhost:5080`.

In a second terminal:

```powershell
Invoke-RestMethod -Method Post http://localhost:5080/api/queue/tickets
Invoke-RestMethod -Method Get http://localhost:5080/api/queue/current
Invoke-RestMethod -Method Post http://localhost:5080/api/queue/clear
Invoke-RestMethod -Method Get http://localhost:5080/api/queue/current
```

Expected: first call returns `{ticketNumber: "A0", issuedAt: <timestamp>}`; second returns the same; third returns `{ticketNumber: "00", issuedAt: null}`; fourth returns `{ticketNumber: "00", issuedAt: null}`. Stop the app (Ctrl+C) after verifying.

- [ ] **Step 8: Run the full backend test suite once more**

Run: `dotnet test backend/Example.QueueSystem.sln` (with `QUEUE_TEST_DB_CONNECTION_STRING` still set as in Task 3 Step 5)
Expected: all 7 tests pass.

- [ ] **Step 9: Commit**

```bash
git add backend/Example.QueueSystem.Api/Dtos backend/Example.QueueSystem.Api/Controllers backend/Example.QueueSystem.Api/Program.cs backend/Example.QueueSystem.Api/appsettings.json backend/Example.QueueSystem.Api/appsettings.Development.json.example
git commit -m "Add queue API controller and wire up DI, CORS, and schema bootstrap"
```

(Do not `git add` the real `appsettings.Development.json` — it's gitignored.)

---

### Task 5: Scaffold Angular Workspace

**Files:**
- Create: `frontend/` (via `ng new`)
- Modify: `frontend/package.json`
- Modify: `frontend/src/index.html`
- Modify: `frontend/src/app/app.ts`
- Modify: `frontend/src/app/app.html`

**Interfaces:**
- Produces: a buildable Angular workspace at `frontend/`, package name `example-com-queue-frontend`, dev server on `http://localhost:4200`, root component reduced to just a `<router-outlet>`.

- [ ] **Step 1: Scaffold the workspace**

Run from the repo root:

```bash
npx --yes @angular/cli@latest new example-com-queue-frontend --directory frontend --routing --style=css --skip-git --package-manager=npm --defaults
```

- [ ] **Step 2: Set the browser tab title**

Modify `frontend/src/index.html` — change the `<title>` element:

```html
<title>Example.com Queue System</title>
```

(Leave the rest of the file as generated.)

- [ ] **Step 3: Reduce the root component to a router outlet**

Overwrite `frontend/src/app/app.ts`:

```typescript
import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {}
```

Overwrite `frontend/src/app/app.html`:

```html
<router-outlet></router-outlet>
```

- [ ] **Step 4: Verify the workspace builds**

Run: `npm run build --prefix frontend`
Expected: build succeeds (output under `frontend/dist/`).

- [ ] **Step 5: Commit**

```bash
git add frontend
git commit -m "Scaffold Angular frontend workspace"
```

---

### Task 6: Shared Styles, Routing, HTTP Client & QueueApiService

**Files:**
- Modify: `frontend/src/styles.css`
- Modify: `frontend/src/app/app.config.ts`
- Create: `frontend/src/app/core/api-config.ts`
- Create: `frontend/src/app/core/queue-api.service.ts`
- Modify: `frontend/src/app/app.routes.ts` (placeholder routes array; real routes wired in Task 9 once all 3 page components exist — see note in Step 4)

**Interfaces:**
- Consumes: the API contract from Task 4 (`POST /api/queue/tickets`, `POST /api/queue/clear`, `GET /api/queue/current`).
- Produces (used by Tasks 7, 8, 9):
  - `interface TicketResponse { ticketNumber: string; issuedAt: string | null }`
  - `class QueueApiService` (providedIn root) with `takeTicket(): Observable<TicketResponse>`, `clearQueue(): Observable<TicketResponse>`, `getCurrent(): Observable<TicketResponse>`.
  - Global CSS classes: `.kiosk-page`, `.kiosk-header`, `.kiosk-button`, `.kiosk-button--primary`, `.kiosk-button--secondary`, `.kiosk-ticket-number`.

- [ ] **Step 1: Add shared kiosk styles**

Append to `frontend/src/styles.css` (keep any generated content above):

```css
* {
  box-sizing: border-box;
}

html, body {
  margin: 0;
  height: 100%;
  font-family: 'Segoe UI', Tahoma, sans-serif;
}

.kiosk-page {
  display: flex;
  flex-direction: column;
  align-items: center;
  min-height: 100vh;
  background: #ffffff;
}

.kiosk-header {
  width: 100%;
  background: #22a559;
  color: #ffffff;
  text-align: center;
  padding: 0.75rem 0;
  font-size: 1rem;
  font-weight: 600;
}

.kiosk-body {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1.5rem;
  width: 100%;
}

.kiosk-button {
  border: none;
  border-radius: 8px;
  color: #ffffff;
  font-size: 1.5rem;
  font-weight: 700;
  padding: 1.5rem 3rem;
  cursor: pointer;
}

.kiosk-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.kiosk-button--primary {
  background: #4472c4;
}

.kiosk-button--secondary {
  background: #a6a6a6;
}

.kiosk-ticket-number {
  font-size: 6rem;
  font-weight: 700;
  margin: 0.5rem 0;
}

.kiosk-footer-button {
  margin-bottom: 3rem;
}

.kiosk-error {
  color: #c0392b;
  font-size: 1.1rem;
}
```

- [ ] **Step 2: Register the HTTP client**

Overwrite `frontend/src/app/app.config.ts`:

```typescript
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(),
  ],
};
```

- [ ] **Step 3: Add the API base URL constant**

Create `frontend/src/app/core/api-config.ts`:

```typescript
export const API_BASE_URL = 'http://localhost:5080/api';
```

- [ ] **Step 4: Add `QueueApiService`**

Create `frontend/src/app/core/queue-api.service.ts`:

```typescript
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';

export interface TicketResponse {
  ticketNumber: string;
  issuedAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class QueueApiService {
  private readonly http = inject(HttpClient);

  takeTicket(): Observable<TicketResponse> {
    return this.http.post<TicketResponse>(`${API_BASE_URL}/queue/tickets`, {});
  }

  clearQueue(): Observable<TicketResponse> {
    return this.http.post<TicketResponse>(`${API_BASE_URL}/queue/clear`, {});
  }

  getCurrent(): Observable<TicketResponse> {
    return this.http.get<TicketResponse>(`${API_BASE_URL}/queue/current`);
  }
}
```

- [ ] **Step 5: Leave routes empty for now**

Confirm `frontend/src/app/app.routes.ts` still contains the generated empty array — it will be filled in at the end of Task 9 once all 3 page components exist:

```typescript
import { Routes } from '@angular/router';

export const routes: Routes = [];
```

- [ ] **Step 6: Verify the workspace still builds**

Run: `npm run build --prefix frontend`
Expected: build succeeds.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/styles.css frontend/src/app/app.config.ts frontend/src/app/core
git commit -m "Add shared kiosk styles, HTTP client, and QueueApiService"
```

---

### Task 7: IT 05-1 — Take Ticket Page

**Files:**
- Create: `frontend/src/app/pages/take-ticket/take-ticket.ts`
- Create: `frontend/src/app/pages/take-ticket/take-ticket.html`
- Create: `frontend/src/app/pages/take-ticket/take-ticket.css`

**Interfaces:**
- Consumes: `QueueApiService.takeTicket()`, `TicketResponse` from Task 6.
- Produces: standalone component `TakeTicket`, selector `app-take-ticket`. On successful take, navigates to `/ticket` passing the issued `TicketResponse` as router state (`{ ticket }`). "ล้างคิว" navigates to `/clear`.

- [ ] **Step 1: Create the component**

Create `frontend/src/app/pages/take-ticket/take-ticket.ts`:

```typescript
import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { QueueApiService } from '../../core/queue-api.service';

@Component({
  selector: 'app-take-ticket',
  imports: [],
  templateUrl: './take-ticket.html',
  styleUrl: './take-ticket.css',
})
export class TakeTicket {
  private readonly queueApi = inject(QueueApiService);
  private readonly router = inject(Router);

  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isSubmitting = signal(false);

  protected onTakeTicket(): void {
    if (this.isSubmitting()) {
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    this.queueApi.takeTicket().subscribe({
      next: (ticket) => {
        this.isSubmitting.set(false);
        this.router.navigate(['/ticket'], { state: { ticket } });
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.errorMessage.set(
          err.status === 409
            ? 'คิวเต็มแล้ว กรุณาล้างคิวก่อนรับบัตรใหม่'
            : 'เกิดข้อผิดพลาด กรุณาลองใหม่อีกครั้ง',
        );
      },
    });
  }

  protected onClearQueue(): void {
    this.router.navigate(['/clear']);
  }
}
```

- [ ] **Step 2: Create the template**

Create `frontend/src/app/pages/take-ticket/take-ticket.html`:

```html
<div class="kiosk-page">
  <div class="kiosk-header">IT 05-1</div>
  <div class="kiosk-body">
    <button class="kiosk-button kiosk-button--primary" (click)="onTakeTicket()" [disabled]="isSubmitting()">
      รับบัตรคิว
    </button>
    @if (errorMessage()) {
      <p class="kiosk-error">{{ errorMessage() }}</p>
    }
  </div>
  <button class="kiosk-button kiosk-button--secondary kiosk-footer-button" (click)="onClearQueue()">
    ล้างคิว
  </button>
</div>
```

- [ ] **Step 3: Create the (empty, component-scoped) stylesheet**

Create `frontend/src/app/pages/take-ticket/take-ticket.css` (empty — all styling comes from the shared global classes):

```css
```

- [ ] **Step 4: Verify the workspace still builds**

Run: `npm run build --prefix frontend`
Expected: build succeeds (the component isn't routed yet, but must still compile standalone).

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/pages/take-ticket
git commit -m "Add IT 05-1 take-ticket page"
```

---

### Task 8: IT 05-2 — Show Ticket Page

**Files:**
- Create: `frontend/src/app/pages/show-ticket/show-ticket.ts`
- Create: `frontend/src/app/pages/show-ticket/show-ticket.html`
- Create: `frontend/src/app/pages/show-ticket/show-ticket.css`

**Interfaces:**
- Consumes: `QueueApiService.getCurrent()`, `TicketResponse` from Task 6.
- Produces: standalone component `ShowTicket`, selector `app-show-ticket`. Reads the ticket passed via router state (falling back to `getCurrent()` if navigated to directly, e.g. on refresh). "กลับไปหน้ารับบัตรคิว" navigates to `/`.

- [ ] **Step 1: Create the component**

Create `frontend/src/app/pages/show-ticket/show-ticket.ts`:

```typescript
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Location, DatePipe } from '@angular/common';
import { QueueApiService, TicketResponse } from '../../core/queue-api.service';

@Component({
  selector: 'app-show-ticket',
  imports: [DatePipe],
  templateUrl: './show-ticket.html',
  styleUrl: './show-ticket.css',
})
export class ShowTicket implements OnInit {
  private readonly queueApi = inject(QueueApiService);
  private readonly router = inject(Router);
  private readonly location = inject(Location);

  protected readonly ticket = signal<TicketResponse | null>(null);

  ngOnInit(): void {
    const state = this.location.getState() as { ticket?: TicketResponse };

    if (state?.ticket) {
      this.ticket.set(state.ticket);
      return;
    }

    this.queueApi.getCurrent().subscribe((current) => this.ticket.set(current));
  }

  protected onBack(): void {
    this.router.navigate(['/']);
  }
}
```

- [ ] **Step 2: Create the template**

Create `frontend/src/app/pages/show-ticket/show-ticket.html`:

```html
<div class="kiosk-page">
  <div class="kiosk-header">IT 05-2</div>
  <div class="kiosk-body">
    <p>หมายเลขคิว</p>
    <p class="kiosk-ticket-number">{{ ticket()?.ticketNumber ?? '00' }}</p>
    @if (ticket()?.issuedAt) {
      <p>วันที่ : {{ ticket()!.issuedAt | date: 'dd/MM/yyyy' }} เวลา {{ ticket()!.issuedAt | date: 'HH:mm' }} น.</p>
    }
  </div>
  <button class="kiosk-button kiosk-button--primary kiosk-footer-button" (click)="onBack()">
    กลับไปหน้ารับบัตรคิว
  </button>
</div>
```

- [ ] **Step 3: Create the (empty) stylesheet**

Create `frontend/src/app/pages/show-ticket/show-ticket.css`:

```css
```

- [ ] **Step 4: Verify the workspace still builds**

Run: `npm run build --prefix frontend`
Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/pages/show-ticket
git commit -m "Add IT 05-2 show-ticket page"
```

---

### Task 9: IT 05-3 — Clear Queue Page & Route Wiring

**Files:**
- Create: `frontend/src/app/pages/clear-queue/clear-queue.ts`
- Create: `frontend/src/app/pages/clear-queue/clear-queue.html`
- Create: `frontend/src/app/pages/clear-queue/clear-queue.css`
- Modify: `frontend/src/app/app.routes.ts`

**Interfaces:**
- Consumes: `QueueApiService.getCurrent()`, `QueueApiService.clearQueue()`, `TicketResponse` from Task 6; `TakeTicket` (Task 7) and `ShowTicket` (Task 8) for route wiring.
- Produces: standalone component `ClearQueue`, selector `app-clear-queue`. Final routes: `/` → `TakeTicket`, `/ticket` → `ShowTicket`, `/clear` → `ClearQueue`, any other path redirects to `/`.

- [ ] **Step 1: Create the component**

Create `frontend/src/app/pages/clear-queue/clear-queue.ts`:

```typescript
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { QueueApiService, TicketResponse } from '../../core/queue-api.service';

@Component({
  selector: 'app-clear-queue',
  imports: [],
  templateUrl: './clear-queue.html',
  styleUrl: './clear-queue.css',
})
export class ClearQueue implements OnInit {
  private readonly queueApi = inject(QueueApiService);
  private readonly router = inject(Router);

  protected readonly ticket = signal<TicketResponse | null>(null);

  ngOnInit(): void {
    this.queueApi.getCurrent().subscribe((current) => this.ticket.set(current));
  }

  protected onClearQueue(): void {
    this.queueApi.clearQueue().subscribe((current) => this.ticket.set(current));
  }

  protected onBack(): void {
    this.router.navigate(['/']);
  }
}
```

- [ ] **Step 2: Create the template**

Create `frontend/src/app/pages/clear-queue/clear-queue.html`:

```html
<div class="kiosk-page">
  <div class="kiosk-header">IT 05-3</div>
  <div class="kiosk-body">
    <button class="kiosk-button kiosk-button--primary" (click)="onClearQueue()">ล้างคิว</button>
    <p>หมายเลขคิวปัจจุบัน</p>
    <p class="kiosk-ticket-number">{{ ticket()?.ticketNumber ?? '00' }}</p>
  </div>
  <button class="kiosk-button kiosk-button--primary kiosk-footer-button" (click)="onBack()">
    กลับไปหน้ารับบัตรคิว
  </button>
</div>
```

- [ ] **Step 3: Create the (empty) stylesheet**

Create `frontend/src/app/pages/clear-queue/clear-queue.css`:

```css
```

- [ ] **Step 4: Wire up the routes**

Overwrite `frontend/src/app/app.routes.ts`:

```typescript
import { Routes } from '@angular/router';
import { TakeTicket } from './pages/take-ticket/take-ticket';
import { ShowTicket } from './pages/show-ticket/show-ticket';
import { ClearQueue } from './pages/clear-queue/clear-queue';

export const routes: Routes = [
  { path: '', component: TakeTicket },
  { path: 'ticket', component: ShowTicket },
  { path: 'clear', component: ClearQueue },
  { path: '**', redirectTo: '' },
];
```

- [ ] **Step 5: Verify the workspace builds**

Run: `npm run build --prefix frontend`
Expected: build succeeds.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app/pages/clear-queue frontend/src/app/app.routes.ts
git commit -m "Add IT 05-3 clear-queue page and wire up routing"
```

---

### Task 10: Root README, Final `.gitignore` Check & End-to-End Manual Verification

**Files:**
- Create: `README.md` (repo root)

**Interfaces:**
- Consumes: nothing new — this task documents and manually verifies the full system built in Tasks 1–9.

- [ ] **Step 1: Write the root README**

Create `README.md` at the repo root:

```markdown
# Example.com Queue System

Queue-ticket kiosk built for interview test No. 5: take a ticket (A0–Z9),
view the issued ticket number, and clear the queue back to "00".

## Prerequisites

- .NET SDK 10.0+
- Node.js 20+ and npm
- PostgreSQL 14+ running locally

## Database setup

Create a dedicated role and databases (run once, as the `postgres` superuser):

\`\`\`sql
CREATE ROLE queue_app LOGIN PASSWORD 'choose-a-password';
CREATE DATABASE queue_system OWNER queue_app;
CREATE DATABASE queue_system_test OWNER queue_app;
\`\`\`

The app creates its own tables on startup (see `QueueSchema.EnsureCreatedAsync`) —
no separate migration step is needed.

## Backend setup

\`\`\`bash
cd backend/Example.QueueSystem.Api
cp appsettings.Development.json.example appsettings.Development.json
# edit appsettings.Development.json and set your real queue_app password
dotnet run
\`\`\`

The API listens on `http://localhost:5080`.

### Running the tests

Unit tests (no database required):

\`\`\`bash
dotnet test backend/Example.QueueSystem.Api.Tests/Example.QueueSystem.Api.Tests.csproj --filter TicketNumberingTests
\`\`\`

Integration tests (require the `queue_system_test` database from the setup step above):

\`\`\`bash
# PowerShell
$env:QUEUE_TEST_DB_CONNECTION_STRING = "Host=localhost;Port=5432;Database=queue_system_test;Username=queue_app;Password=your-password"
dotnet test backend/Example.QueueSystem.sln
\`\`\`

## Frontend setup

\`\`\`bash
cd frontend
npm install
npm start
\`\`\`

Open `http://localhost:4200`. The backend must already be running on `http://localhost:5080`.

## Screens

- **IT 05-1** (`/`) — take a ticket, or go to the clear-queue screen.
- **IT 05-2** (`/ticket`) — shows the ticket just issued (number + date/time).
- **IT 05-3** (`/clear`) — shows the current ticket number and clears the queue.

## Limitations

- The queue does **not** wrap after `Z9`. Once `Z9` has been issued, taking a
  new ticket returns an error until "ล้างคิว" (Clear Queue) is pressed. This
  was a deliberate scope decision, since the requirement doc did not specify
  post-`Z9` behavior.
- Single fixed queue only — no multi-counter or multi-service-type support.
- No authentication — the kiosk is assumed to run in a trusted, physically
  controlled environment.
\`\`\`

- [ ] **Step 2: Confirm no secrets are staged**

Run: `git status`
Expected: `backend/Example.QueueSystem.Api/appsettings.Development.json` does NOT appear (it's gitignored). If it does appear, stop and fix `.gitignore` before continuing.

- [ ] **Step 3: End-to-end manual verification**

With PostgreSQL running and the `queue_system` database set up:

1. Terminal 1: `dotnet run --project backend/Example.QueueSystem.Api`
2. Terminal 2: `npm start --prefix frontend`
3. Open `http://localhost:4200` in a browser.
4. On IT 05-1, click "รับบัตรคิว" → confirm navigation to IT 05-2 showing ticket `A0` and a date/time.
5. Click "กลับไปหน้ารับบัตรคิว" → confirm back on IT 05-1.
6. Click "รับบัตรคิว" again → confirm ticket `A1` on IT 05-2, then go back.
7. From IT 05-1, click "ล้างคิว" → confirm navigation to IT 05-3, showing current ticket `A1`.
8. Click "ล้างคิว" on IT 05-3 → confirm the displayed number changes to `00`.
9. Click "กลับไปหน้ารับบัตรคิว" → confirm back on IT 05-1.
10. Click "รับบัตรคิว" → confirm the next ticket is `A0` again (queue restarted after clear).

Fix any discrepancy before proceeding. Stop both servers (Ctrl+C in each terminal) once verified.

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "Add root README with setup, testing, and limitations"
```

---

## After this plan

Pushing to the GitLab/GitHub remote is a separate, explicit step — confirm the remote URL and get the user's go-ahead before running `git push`, per the earlier discussion about the GitHub vs. GitLab.com mismatch in the requirement doc.
