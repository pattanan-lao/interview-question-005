# Example.com Queue System

Queue-ticket kiosk built for interview test No. 5: take a ticket (A0–Z9),
view the issued ticket number, and clear the queue back to "00".

## Architecture

The backend follows Clean Architecture with 4 projects (dependencies point
inward, `Domain` at the center):

- `Example.QueueSystem.Domain` — pure ticket-numbering rules, no dependencies.
- `Example.QueueSystem.Application` — ports (`IQueueRepository`) and the
  use case (`IQueueService`/`QueueService`) the API calls.
- `Example.QueueSystem.Infrastructure` — EF Core (Npgsql) implementation of
  `IQueueRepository`: `QueueDbContext`, the entities, and the migrations.
- `Example.QueueSystem.Api` — composition root (DI, CORS) and HTTP controllers.
  Controllers depend on `IQueueService` only, never on the repository directly.

## Run with Docker (quickest)

Requires Docker Desktop (or Docker Engine + Compose v2). Nothing else — no
.NET SDK, Node, or PostgreSQL on the host.

```bash
cp .env.example .env
# edit .env and set POSTGRES_PASSWORD
docker compose up --build
```

Open `http://localhost:4200`.

Three containers come up: PostgreSQL, the API, and nginx serving the Angular
build. nginx also reverse-proxies `/api` to the API container, so the browser
only ever talks to port 4200 — the API is not published on the host. To call
it directly, go through the proxy:

```bash
curl http://localhost:4200/api/queue/current
```

PostgreSQL is published on host port **15432** so you can `psql` in or point
the integration tests at it. The port is deliberately far from 5432/5433,
which a local PostgreSQL install usually occupies; override it with
`POSTGRES_HOST_PORT` in `.env` if 15432 is taken too. Data lives in the
`queue_pgdata` named volume and survives `docker compose down`; use
`docker compose down -v` to wipe the queue back to a clean state.

> **Port clashes on Windows are silent.** If a local PostgreSQL already holds
> the published port on `0.0.0.0`, Docker will bind only `::` and start without
> complaint — then `Host=localhost` reaches your *local* server and fails with
> `28P01: password authentication failed`. If you see that, the port is taken;
> pick another via `POSTGRES_HOST_PORT`.

To run everything on the host instead — with Angular hot-reload — follow the
manual setup below.

## Prerequisites (manual setup)

- .NET SDK 10.0+
- Node.js 22.22.3+ (or 24.15+/26+) and npm — the minimum Angular 22 requires
- PostgreSQL 14+ running locally

## Database setup

Create a dedicated role and databases (run once, as the `postgres` superuser):

```sql
CREATE ROLE queue_app LOGIN PASSWORD 'choose-a-password';
CREATE DATABASE queue_system OWNER queue_app;
CREATE DATABASE queue_system_test OWNER queue_app;
```

The app applies its EF Core migrations on startup (see
`QueueDbInitializer.MigrateAsync`), creating the tables and seeding the singleton
`queue_state` row — no separate migration step is needed.

> **Upgrading from a pre-EF-Core database.** Earlier versions created the tables
> with raw SQL and no `__EFMigrationsHistory`, and their `queue_state` has no
> `version` column, so the schema cannot be baselined. Migrating an existing
> database will fail with `relation "queue_state" already exists`. Drop the old
> tables (or `docker compose down -v` for the Docker setup) and let the
> migration recreate them.

### Changing the schema

The migrations live in `backend/Example.QueueSystem.Infrastructure/Migrations`.
After editing `QueueDbContext` or an entity, scaffold the diff (requires
`dotnet tool install --global dotnet-ef`):

```bash
cd backend
dotnet ef migrations add YourMigrationName \
  --project Example.QueueSystem.Infrastructure \
  --startup-project Example.QueueSystem.Infrastructure \
  --output-dir Migrations
```

Scaffolding never opens a connection — `DesignTimeQueueDbContextFactory` supplies
a placeholder connection string. For commands that do reach the database
(`dotnet ef database update`, `migrations remove` on an applied migration), set
`QUEUE_DB_CONNECTION_STRING` first. Applying migrations by hand is optional in
normal use: the API runs them at startup.

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
the test fixture calls `QueueDbInitializer.ResetAsync`, which drops the database and
rebuilds it from the migrations, so pointing it at `queue_system` (the real dev
database) would destroy your dev data.

```bash
# PowerShell
$env:QUEUE_TEST_DB_CONNECTION_STRING = "Host=localhost;Port=5432;Database=queue_system_test;Username=queue_app;Password=your-password"
dotnet test backend/Example.QueueSystem.sln
```

If you are using the Docker setup, `queue_system_test` is created for you on
first startup — point the tests at port **15432** (or your `POSTGRES_HOST_PORT`)
and your `.env` password instead:

```bash
# PowerShell
$env:QUEUE_TEST_DB_CONNECTION_STRING = "Host=localhost;Port=15432;Database=queue_system_test;Username=queue_app;Password=your-.env-password"
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

Open `http://localhost:4200`. The backend must already be running on
`http://localhost:5080`. The app calls the API at the relative path `/api`;
`frontend/proxy.conf.json` forwards that to port 5080 during `ng serve`, and
nginx does the same job in the Docker setup.

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
