# Realtime events

Related: [../01-architecture/REALTIME-ARCHITECTURE.md](../01-architecture/REALTIME-ARCHITECTURE.md)

Event payload example:

```json
{
  "type": "ticket.assigned",
  "occurredAtUtc": "2026-09-02T07:00:00Z",
  "id": "guid",
  "businessNumber": "INC-2026-000001"
}
```

Client invalidates `['tickets', id]` query.

Types catalog (initial): `ticket.updated`, `ticket.assigned`, `notification.created`, `remote.requested`, `remote.consented`, `remote.ended`, `sla.warned`, `change.approvalRequired`.

No PII-heavy bodies on the wire.
