# Incident management

Related: [SERVICE-DESK.md](SERVICE-DESK.md) · [PROBLEM-MANAGEMENT.md](PROBLEM-MANAGEMENT.md) · [EVENT-MANAGEMENT.md](EVENT-MANAGEMENT.md) · [SECURITY-MANAGEMENT.md](SECURITY-MANAGEMENT.md)

## Definition

An **incident** is an unplanned interruption or degradation of a service or CI. It is a `Ticket` with `TicketType = Incident`.

A **security incident** is an incident with security classification. Same aggregate, additional fields (Tactics, data involved, notification required), **stricter permissions**, default internal comments.

## From events

Monitoring creates `OperationalEvent`. A rule or human **promotes** to incident. Promotion stores `EventId` links. Not every backup failure is an incident (e.g. failed job retried successfully).

## Major incident

Flag `IsMajor` with war-room notes, comms owner, business service. Separate dashboard widget. Still a ticket, not a new system.

## Lifecycle extras vs generic ticket

- Impacted CIs and business services
- Workaround
- Customer communications log
- Link to Problem when repeating
- Link to Change for fix
- Post-incident review for major (document, may be later phase)

## Metrics (server-side)

MTTA, MTTR, major count, reopen rate, recurring (same CI + category window).

## What incident is not

- A problem record
- A change record
- A vulnerability (vuln may **cause** an incident)
