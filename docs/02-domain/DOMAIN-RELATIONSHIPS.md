# Domain relationships

Related: [DOMAIN-MODEL.md](DOMAIN-MODEL.md) · [../06-data/RELATIONSHIP-CATALOG.md](../06-data/RELATIONSHIP-CATALOG.md)

## Canonical chain

```
User (employee) —owns/uses— Asset —related— ConfigurationItem
                                      |
Ticket —affects— CI —hosted-on— Server CI —depends-on— Network CI
  |                 |
  |                 +— Application CI —owned-by— Department
  |                                   —supplied-by— Vendor
  |                                   —has— SLA / RTO / RPO on BusinessService
  |
  +— RemoteSessionRequest —target— CI —engine node id
  |
  +— (if incident) —related— Problem —requires— ChangeRequest —affects— CIs
                                              —approvals—
                                              —implementation evidence—
```

## Governance chain

```
InternalControl —mapped-to— FrameworkRequirement (many frameworks)
       |
       +— ControlAssessment (period)
       +— Evidence (reuse)
       +— Finding — CorrectiveAction
       +— PolicyException
       +— ManagedDocument (policy implementing control)
```

## Required vs optional links

| From | To | Cardinality | Required? |
|------|----|-------------|-----------|
| Ticket | Requester User | 1 | Yes |
| Ticket | CIs | 0..n | Optional but strongly encouraged for incidents |
| RemoteSessionRequest | Ticket | 0..1 | Required for attended employee support; optional for some unattended if Change linked |
| RemoteSessionRequest | ChangeRequest | 0..1 | Unattended often requires ticket **or** change |
| RemoteSessionRequest | Target CI | 1 | Yes |
| ChangeRequest | CIs | 1..n | Yes for implementable changes |
| Problem | Incidents (Tickets) | 1..n | Yes |
| Incident Ticket | OperationalEvent | 0..n | Optional |
| Asset | CI | 0..1 | If operational |
| BusinessService | CIs | 1..n | Yes when declared |
| Evidence | InternalControl | 0..n | Typical |
| Finding | AuditEngagement | 0..1 | If from audit |
| AccessCase | User | 1 | Subject of JML |
| Contract | Vendor | 1 | Yes |
| DrTest | BusinessService or CI | 1..n | Yes |

## Ownership

Every InternalControl, CI, Ticket queue, Policy, and Risk has an **owner** (user and/or role/department). Ownership is used for reminders and resource-level auth, not as a substitute for permissions.

## Interface / integration CIs

`SystemInterface` is a **CI type** (or subtype fields on CI), not a disconnected register. Governance “System Interface Register” is a **view** on CIs of that type.
