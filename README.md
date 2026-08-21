# Example.com Queue System

Queue-ticket kiosk built for interview test No. 5: take a ticket (A0–Z9),
view the issued ticket number, and clear the queue back to "00".

## Architecture

The backend follows Clean Architecture with 4 projects (dependencies point
inward, `Domain` at the center):

- `Example.QueueSystem.Domain` — pure ticket-numbering rules, no dependencies.
- `Example.QueueSystem.Application` — ports (`IQueueRepository`) and the
  use case (`IQueueService`/`QueueService`) the API calls.
- `Example.QueueSystem.Infrastructure` — PostgreSQL implementation of
  `IQueueRepository`, plus schema bootstrap.
- `Example.QueueSystem.Api` — composition root (DI, CORS) and HTTP controllers.
  Controllers depend on `IQueueService` only, never on the repository directly.

## Prerequisites

- .NET SDK 10.0+
- Node.js 20+ and npm
- PostgreSQL 14+ running locally

## Database setup

Create a dedicated role and databases (run once, as the `postgres` superuser):

```sql
CREATE ROLE queue_app LOGIN PASSWORD 'choose-a-password';
CREATE DATABASE queue_system OWNER queue_app;
CREATE DATABASE queue_system_test OWNER queue_app;
```

The app creates its own tables on startup (see `QueueSchema.EnsureCreatedAsync`) —
no separate migration step is needed.

## Backend setup

```bash
cd backend/Example.QueueSystem.Api
cp appsettings.Development.json.example appsettings.Development.json
# edit appsettings.Development.json and set your real queue_app password
dotnet run
```

The API listens on `http://localhost:5080`.

### Running the tests

Unit tests (no database required):

```bash
dotnet test backend/Example.QueueSystem.Domain.Tests/Example.QueueSystem.Domain.Tests.csproj
```

Integration tests (require the `queue_system_test` database from the setup step above).
**Warning:** point `QUEUE_TEST_DB_CONNECTION_STRING` at a disposable database only —
the test fixture calls `QueueSchema.ResetAsync`, which drops and recreates tables, so
pointing it at `queue_system` (the real dev database) would destroy your dev data.

```bash
# PowerShell
$env:QUEUE_TEST_DB_CONNECTION_STRING = "Host=localhost;Port=5432;Database=queue_system_test;Username=queue_app;Password=your-password"
dotnet test backend/Example.QueueSystem.sln
```

Frontend tests:

```bash
npm test --prefix frontend
```

## Frontend setup

```bash
cd frontend
npm install
npm start
```

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
