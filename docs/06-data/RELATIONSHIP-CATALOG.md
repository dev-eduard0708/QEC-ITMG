# Relationship catalog

Major FKs and link tables. Cardinality abbreviated.

| From | To | Type | Notes |
|------|----|------|-------|
| UserRole | User, Role | n:1 | |
| Ticket | User (requester, assignee) | n:1 | |
| TicketCi | Ticket, CI | n:n | |
| Ticket | Problem | n:1 | optional |
| ProblemIncident | Problem, Ticket | n:n | |
| Ticket | OperationalEvent | n:n | |
| RemoteSessionRequest | Ticket, Change, CI, User | n:1 | ticket or change rule |
| RemoteSession | Request | 1:1 | |
| ChangeCi | Change, CI | n:n | |
| Asset | CI | n:1 | optional |
| Asset | User, Location, Vendor | n:1 | |
| CI | Vendor, Department, Location, CiType | n:1 | |
| CiRelationship | CI, CI | n:n | typed |
| BusinessServiceCi | Service, CI | n:n | |
| ControlMapping | Control, Requirement | n:n | |
| ControlAssessment | Control | n:1 | |
| EvidenceLink | Evidence, targets | n:n | |
| Finding | Audit, Control | n:1 | |
| CorrectiveAction | Finding | n:1 | |
| Contract | Vendor | n:1 | |
| AccessCase | User (subject) | n:1 | |
| ManagedDocument | User (owner) | n:1 | |
| PolicyAcknowledgement | DocumentVersion, User | n:n | |
| Notification | User | n:1 | |
| Attachment | polymorphic parent | n:1 | |
| DrTest | BusinessService | n:1 | |
| Vulnerability | CI | n:1 | |
| Risk | CI / Control | optional | |
| Exception | Control / Policy | | |

Full business graph: [../02-domain/DOMAIN-RELATIONSHIPS.md](../02-domain/DOMAIN-RELATIONSHIPS.md).
