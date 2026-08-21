# Queue Ticket System — Design

## Source requirement

Interview test No. 5 (`แบบทดสอบ No.5.docx`): build a queue-ticket kiosk
program with three screens (IT 05-1, IT 05-2, IT 05-3), submit as a
GitLab repo named `interview-question-005`, and name websites/packages
as if working at a company called `example.com`.

## Stack

- Backend: ASP.NET Core Web API, targeting `net10.0` (matches installed
  SDK 10.0.400; default C# language version, no LangVersion pin)
- Frontend: Angular
- Database: PostgreSQL (assumed installed locally; no Docker)
- Repo: monorepo, `/backend` and `/frontend`

## Naming

- Solution: `Example.QueueSystem.sln`
- Backend root namespace: `Example.QueueSystem` (per-project suffix: `.Domain`, `.Application`, `.Infrastructure`, `.Api`)
- Frontend package name: `example-com-queue-frontend`
- Browser title: "Example.com Queue System"

## Backend architecture (Clean Architecture)

The backend is split into 4 projects so dependencies point inward, per
Clean Architecture's Dependency Rule — inner layers know nothing about
outer layers:

```
Example.QueueSystem.Api  ──────────────┐
        │  (composition root: DI, HTTP)│
        ▼                              ▼
Example.QueueSystem.Application   Example.QueueSystem.Infrastructure
   (ports + use cases,                 │  (implements the ports)
    no project references)             ▼
                              Example.QueueSystem.Domain
                           (pure business rules, zero dependencies)
```

- **`Example.QueueSystem.Domain`** — pure business rules, no project or
  package references. Owns ticket-numbering logic (`TicketNumbering`,
  `QueuePosition`, `NextTicketOutcome`, `NextTicketResult`). Fully unit
  testable with no database.
- **`Example.QueueSystem.Application`** — no project references of its
  own (pure C# on top of the BCL). Defines the port `IQueueRepository`
  (and its result types `TakeTicketResult`, `CurrentQueueState`) that
  outer layers implement, and the use-case interface/implementation
  `IQueueService`/`QueueService` that the API layer calls. `QueueService`
  is a thin orchestrator today (it delegates straight to
  `IQueueRepository`); it exists so business rules that aren't purely
  numeric (e.g. future validation, logging, notifications) have a layer
  to live in without the API talking to persistence directly. It works
  entirely in terms of the repository's result types rather than
  `Domain` types, since the numbering decision is made inside
  Infrastructure's transaction — so Application has no reason to
  reference `Domain`, and doesn't. Because it currently contains no
  branching logic of its own, it is exercised through the Infrastructure
  integration tests and the API smoke test rather than a redundant
  dedicated unit test suite.
- **`Example.QueueSystem.Infrastructure`** — references `Application`
  (to implement `IQueueRepository`) and `Domain` (to compute the next
  ticket). Implements `IQueueRepository` against PostgreSQL
  (`QueueRepository`, using `Domain.TicketNumbering` for the actual
  numbering decision inside its transaction) and owns schema bootstrap
  (`QueueSchema`).
- **`Example.QueueSystem.Api`** — references `Application` and
  `Infrastructure`. This is the only project allowed to reference
  `Infrastructure`, and only for composition-root DI registration in
  `Program.cs` — controllers depend on `IQueueService` from `Application`,
  never on `IQueueRepository`/`QueueRepository` directly. Owns
  `QueueController` and the HTTP DTOs.

Test projects mirror this: `Example.QueueSystem.Domain.Tests` (pure unit
tests, no DB) and `Example.QueueSystem.Infrastructure.Tests` (integration
tests against a real local PostgreSQL, covering the concurrency-safety
requirement end to end through the real `QueueRepository`).

## Screens

### IT 05-1 — Take Ticket
- Button "รับบัตรคิว" (Take Queue Ticket) → calls `POST /api/queue/tickets`,
  then navigates to IT 05-2 with the issued ticket.
- Button "ล้างคิว" (Clear Queue) → navigates to IT 05-3 (does **not**
  clear by itself — IT 05-3 owns the actual clear action, matching the
  mockup where IT 05-3 has its own "ล้างคิว" button).

### IT 05-2 — Show Ticket Number
- Displays `หมายเลขคิว` (Queue Number) and the issued date/time.
- On load, prefers the ticket passed via client router state (from IT 05-1)
  so a page refresh keeps showing *this* ticket rather than whatever the
  queue's current state has become; falls back to `GET /api/queue/current`
  only when no router state is present (e.g. direct navigation to this
  screen), so the screen is never left blank.
- Button "กลับไปหน้ารับบัตรคิว" (Back to Take Ticket) → navigates to IT 05-1.

### IT 05-3 — Clear Queue
- On load, calls `GET /api/queue/current` and displays it as
  `หมายเลขคิวปัจจุบัน` (Current Queue Number).
- Button "ล้างคิว" (Clear Queue) → calls `POST /api/queue/clear`,
  refreshes the displayed number to `00`.
- Button "กลับไปหน้ารับบัตรคิว" (Back to Take Ticket) → navigates to IT 05-1.

## Data model (PostgreSQL)

### `queue_state` (single row, id fixed = 1)
| column               | type        | notes                              |
|-----------------------|-------------|-------------------------------------|
| id                    | smallint PK | always `1`                          |
| current_letter_index  | smallint    | 0–25 (A–Z), `NULL` = no ticket yet   |
| current_digit         | smallint    | 0–9, `NULL` = no ticket yet          |
| updated_at            | timestamptz | last change time                    |

`NULL`/`NULL` is the "00" display state — both the initial state before
any ticket is issued, and the state immediately after clearing.

### `queue_tickets` (append-only history)
| column        | type         | notes                          |
|---------------|--------------|---------------------------------|
| id            | bigserial PK |                                  |
| ticket_number | text         | e.g. `"A5"`                     |
| issued_at     | timestamptz  |                                  |

Used to show the issued date/time on IT 05-2, and as an audit trail.

## Ticket numbering rules

- Sequence: `A0, A1, …, A9, B0, B1, …, Z9` (26 letters × 10 digits = 260
  tickets per cycle).
- Digit rolls 0→9 then letter increments and digit resets to 0 (e.g.
  `A9` → `B0`, per the requirement doc's example).
- After `Z9` is issued, further take-ticket requests are rejected
  (HTTP 409) rather than wrapping back to `A0`. This is a deliberate
  scope decision (see Limitations below), not an oversight.
- "Clear Queue" resets `queue_state` to `NULL`/`NULL` (display `00`);
  the next ticket taken after a clear starts again at `A0`.

## Concurrency safety

Ticket issuance runs inside a single PostgreSQL transaction:

1. `SELECT current_letter_index, current_digit FROM queue_state WHERE id = 1 FOR UPDATE`
   — takes a row lock, so concurrent requests serialize on this single row.
2. Compute the next ticket in application code.
3. If already at `Z9`, roll back and return 409.
4. Otherwise `UPDATE queue_state ...` and `INSERT INTO queue_tickets ...`.
5. Commit.

This guarantees no two concurrent "take ticket" requests can receive the
same ticket number or skip a number, satisfying the requirement doc's
"must prevent simultaneous ticket-taking" note.

## API

| Method | Path                   | Behavior                                                                 |
|--------|-------------------------|---------------------------------------------------------------------------|
| POST   | `/api/queue/tickets`   | Issue next ticket. Returns `{ ticketNumber, issuedAt }`, or 409 if exhausted (`Z9` already issued). |
| POST   | `/api/queue/clear`     | Reset state to "00". Returns `{ ticketNumber: "00" }`.                   |
| GET    | `/api/queue/current`   | Current display state without mutating: `{ ticketNumber, issuedAt }` (`ticketNumber` is `"00"`, `issuedAt` is `null`, when state is unset/cleared). |

## Testing

- **Unit tests** (`Example.QueueSystem.Domain.Tests`, xUnit, no DB): pure
  ticket-increment logic — `A0→A1`, `A9→B0`, `Z9→exhausted`, clear resets
  to `NULL`.
- **Integration tests** (`Example.QueueSystem.Infrastructure.Tests`,
  xUnit, requires a live local PostgreSQL via a connection string in test
  config): fire N concurrent take-ticket requests and assert the
  resulting ticket set has no duplicates and no gaps. Documented in the
  README as requiring a real DB to run, since local dev was chosen to be
  Docker-free.

## Limitations (documented in README)

- Queue does not wrap after `Z9`; it blocks further ticket issuance
  until "Clear Queue" is pressed. This was an explicit choice (per
  clarification) since the requirement doc did not specify post-`Z9`
  behavior.
- Single fixed queue (no multi-counter/multi-service-type support) —
  out of scope for this test.

## Out of scope

- Authentication/authorization (kiosk is assumed to run in a trusted,
  physically-controlled environment).
- Multi-language i18n beyond the Thai UI text taken from the mockups.
