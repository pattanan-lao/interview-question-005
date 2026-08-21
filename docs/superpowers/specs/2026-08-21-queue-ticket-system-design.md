# Queue Ticket System — Design

## Source requirement

Interview test No. 5 (`แบบทดสอบ No.5.docx`): build a queue-ticket kiosk
program with three screens (IT 05-1, IT 05-2, IT 05-3), submit as a
GitLab repo named `interview-question-005`, and name websites/packages
as if working at a company called `example.com`.

## Stack

- Backend: ASP.NET Core Web API, C# 10, targeting `net6.0`
- Frontend: Angular
- Database: PostgreSQL (assumed installed locally; no Docker)
- Repo: monorepo, `/backend` and `/frontend`

## Naming

- Solution: `Example.QueueSystem.sln`
- Backend namespace/root: `Example.QueueSystem.Api`
- Frontend package name: `example-com-queue-frontend`
- Browser title: "Example.com Queue System"

## Screens

### IT 05-1 — Take Ticket
- Button "รับบัตรคิว" (Take Queue Ticket) → calls `POST /api/queue/tickets`,
  then navigates to IT 05-2 with the issued ticket.
- Button "ล้างคิว" (Clear Queue) → navigates to IT 05-3 (does **not**
  clear by itself — IT 05-3 owns the actual clear action, matching the
  mockup where IT 05-3 has its own "ล้างคิว" button).

### IT 05-2 — Show Ticket Number
- Displays `หมายเลขคิว` (Queue Number) and the issued date/time.
- On load, calls `GET /api/queue/current` (resilient to page refresh /
  direct navigation, not solely reliant on client router state).
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

- **Unit tests** (xUnit, no DB): pure ticket-increment logic — `A0→A1`,
  `A9→B0`, `Z9→exhausted`, clear resets to `NULL`.
- **Integration tests** (xUnit, requires a live local PostgreSQL via a
  connection string in test config): fire N concurrent take-ticket
  requests and assert the resulting ticket set has no duplicates and no
  gaps. Documented in the README as requiring a real DB to run, since
  local dev was chosen to be Docker-free.

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
