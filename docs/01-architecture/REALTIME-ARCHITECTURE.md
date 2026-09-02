# Realtime architecture

Related: [BACKEND-ARCHITECTURE.md](BACKEND-ARCHITECTURE.md) · [../07-api/REALTIME-EVENTS.md](../07-api/REALTIME-EVENTS.md)

## Purpose

SignalR improves **operational awareness**: queue changes, remote session status, notification counts, major incident banners. It is **not** the system of record. Refresh and TanStack Query invalidation remain correct without SignalR.

## Hubs (planned)

| Hub | Who may join | Events |
|-----|--------------|--------|
| `user` | Authenticated user group `user:{id}` | Notification created, ticket comment if watcher |
| `servicedesk` | `ticket.read` | Ticket assigned/updated in permitted queues |
| `remotesupport` | `remote.request` or session participant | Session requested, consented, started, ended |
| `ops` | `event.read` | High-severity operational events (throttled) |
| `change` | `change.read` | Approval required (to approver group) |

Do not broadcast ticket internals (internal notes) to hubs that employees can join.

## Authorization

On connect: authenticated. On group add: server-side permission + queue membership. Client cannot subscribe to arbitrary ticket ids.

## Payload rules

- Small: id, business number, status, timestamp, event type
- Clients fetch full DTO via REST
- No attachment contents, no secrets, no evidence files

## Scale

Single host: in-memory SignalR. Multiple hosts: SQL Server or Redis backplane **when** a second app node is introduced. Document the switch in operations; do not add Redis in Phase 0.

## Failure

If SignalR fails, UI still works. Show “live updates unavailable” only in IT workspace, not as a blocking error.
