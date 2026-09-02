# Shared kernel

Related: [../01-architecture/MODULAR-MONOLITH.md](../01-architecture/MODULAR-MONOLITH.md) · [DOMAIN-MODEL.md](DOMAIN-MODEL.md)

## What belongs here

Types and services used by **all** modules without importing those modules.

| Item | Notes |
|------|-------|
| `EntityId` / strongly typed ids (`TicketId`, …) | Optional; GUID aliases acceptable if consistent |
| `IClock`, `DateTimeOffset` UTC helpers | Tests inject clock |
| `Result` / error codes | No HTTP types in domain |
| `ICurrentUser` | Id, name, permissions, timezone |
| `IAuthorizationService` resource checks | |
| `INumberingService` | [IDENTIFIERS-AND-NUMBERING.md](IDENTIFIERS-AND-NUMBERING.md) |
| `IAttachmentService` | |
| `ICommentService` / `ITimeline` | |
| `IAuditHistoryWriter` | |
| `ISecurityAuditLogger` | |
| `IWorkflowService` | |
| `INotificationPublisher` | Fire event; Notifications module sends |
| `DataClassification` | Public, Internal, Confidential, Restricted |
| `SoftDelete` conventions | Interface `ISoftDeletable` |
| `IHasRowVersion` | |
| Pagination types | Query side |

## What does not belong here

- Ticket, Change, Control entities
- MeshCentral client
- EF configurations for module tables
- React concerns

## Workflow engine (scoped)

Definition data:

- `WorkflowType` (Ticket, Change, AccessCase, Policy, Finding, Exception, VendorAssessment, RemoteSessionRequest)
- States, transitions, `RequiredPermission`, `RequiresComment`, `RequiresMfa` (privileged)
- Optional `ApproverRole`

Instance: current state, started at, completed at.

MVP implements Ticket + Change + Remote consent with this engine. Other types onboard without a new framework.

Not in scope: arbitrary BPMN designer for business users in MVP.

## Comments vs internal notes

`Comment` has `Visibility = Public | Internal`. Employees never receive Internal. Security incidents default Internal unless explicitly released.

## Domain events (in-process)

Examples: `TicketAssigned`, `SlaBreached`, `RemoteConsentGranted`, `ChangeApproved`, `EvidenceExpired`. Handlers in other modules must be resilient and idempotent.
