# Identifiers and numbering

## Technical ids

- Primary keys: GUID
- Prefer time-ordered GUIDs to reduce index fragmentation
- Never expose sequential integers as the only id (enumeration attacks)

## Business numbers

Format: `{PREFIX}-{YEAR}-{SEQ}` with `SEQ` 6 digits, year = UTC year of allocation.

| Prefix | Aggregate | Example |
|--------|-----------|---------|
| INC | Ticket (incident) | INC-2026-000001 |
| SR | Ticket (service request) | SR-2026-000001 |
| INQ | Ticket (inquiry, optional) | INQ-2026-000001 |
| PRB | Problem | PRB-2026-000001 |
| CHG | ChangeRequest | CHG-2026-000001 |
| EVT | OperationalEvent | EVT-2026-000001 |
| RS | RemoteSession / request (see below) | RS-2026-000001 |
| TCK | Generic ticket if type unknown — **do not use**; always INC/SR |
| AUD | AuditEngagement | AUD-2026-000001 |
| FND | Finding | FND-2026-000001 |
| RISK | Risk | RISK-2026-000001 |
| CTRL | InternalControl | CTRL-IAM-001 (see controls) |
| EVD | Evidence | EVD-2026-000001 |
| AC | AccessCase | AC-2026-000001 |
| AST | Asset | AST-2026-000001 |
| CI | ConfigurationItem | CI-2026-000001 |
| VND | Vendor | VND-2026-000001 |
| POL | ManagedDocument (policy) | POL-2026-000001 |
| CA | CorrectiveAction | CA-2026-000001 |
| EXC | PolicyException | EXC-2026-000001 |

### Control numbers

`CTRL-{DOMAIN}-{NNN}` e.g. `CTRL-IAM-004`. Domain codes are lookup data (IAM, CHG, OPS, BCM, …), not hardcoded enums in multiple places.

### Remote numbers

One sequence `RS` for `RemoteSessionRequest`. When a session starts, it **keeps** the request number or gets `RS` child suffix. Decision: **request and session share RS number**; session is a child entity. Avoid RS-request vs RS-session confusion.

## Concurrency-safe generation

Table `plt.NumberSequence` (`Prefix`, `Year`, `LastValue`) with transaction:

1. `BEGIN TRAN`
2. `SELECT ... FROM plt.NumberSequence WITH (UPDLOCK, HOLDLOCK) WHERE Prefix=@p AND Year=@y`
3. Insert year row if missing (`LastValue = 0`)
4. Increment, format, assign to aggregate
5. Commit with aggregate insert

Alternatively SQL `SEQUENCE` objects per prefix-year — harder for new years. Prefer table + UPDLOCK.

Do **not** generate numbers with `MAX+1` without locks.

Gaps are allowed (rollback after increment is acceptable). Uniqueness is mandatory.

## Display

UI shows business number as primary. GUID in copy-debug for admins.
