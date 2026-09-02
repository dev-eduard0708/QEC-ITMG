# Modular monolith

Related: [SYSTEM-ARCHITECTURE.md](SYSTEM-ARCHITECTURE.md) · [ADR-0004](../12-decisions/ADR-0004-modular-monolith.md)

## Decision

Ship **one ASP.NET Core host** and **one SQL Server database** with **module assemblies** and **schema grouping** (EF schemas or table prefixes by module). Communication between modules is:

1. **In-process** method/interface for strongly owned queries (prefer)
2. **Domain/integration events** (in-process bus) for side effects (notify, timeline, evidence hooks)
3. **Database FK** across modules only for shared kernel and published identifiers — avoid hidden joins that bypass APIs for writes

No network hop between QEC ITMG modules in v1.

## Module catalog

| Module | Owns (writes) | May reference |
|--------|---------------|---------------|
| Identity | Users, credentials link, auth sessions | — |
| Organization | Departments, locations, org units | Identity |
| Platform | Number sequences, attachments metadata, comments, workflow instances, business audit history | Identity |
| Notifications | Notification, templates, deliveries | Identity, events from all |
| Cmdb | Assets, CIs, relationships, business services | Organization, Identity, ThirdParty (vendor id) |
| ServiceDesk | Tickets, SLA clocks, knowledge | Cmdb, Identity, Platform |
| ChangeManagement | Changes, CAB-related records | Cmdb, ServiceDesk (links), Identity |
| RemoteSupport | Session requests, consent, session audit | ServiceDesk, Cmdb, Identity |
| AccessManagement | JML cases, access requests, reviews | Identity, Organization, Cmdb (apps as CIs) |
| ItOperations | Events (ops), backups, patches, certs, jobs | Cmdb |
| SecurityManagement | Vulns, security extras, risk, exceptions | Cmdb, ServiceDesk (security incidents) |
| Governance | Registers metadata, diagrams metadata, control **hosting** coordination | Cmdb, Compliance |
| PolicyDocuments | Policies, document sets, acknowledgements | Identity, Compliance (control links) |
| Compliance | Frameworks, requirements, mappings, assessments | Governance/controls, Evidence |
| Evidence | Evidence records and links | Platform attachments, any linked aggregate by typed FKs + justified polymorphic link |
| AuditManagement | Audits, findings, CAPA | Evidence, Compliance, Identity |
| BusinessContinuity | BIA, plans, DR tests, RTO/RPO on services | Cmdb |
| ThirdParty | Vendors, contracts, vendor assessments | Cmdb, Identity |
| Reporting | Read models / query services | All (read-only) |
| Administration | Roles, permissions, lookups, settings | Identity |

**Internal Control** lives in Compliance/Governance as specified in [../05-compliance/CONTROL-MODEL.md](../05-compliance/CONTROL-MODEL.md): one control entity, not per-module copies.

## Dependency rules (enforced later by architecture tests)

- Domain layer of a module must not reference another module’s infrastructure
- No module references Reporting for writes
- RemoteSupport must not be callable without Identity authorization
- ServiceDesk may link to Change; Change must not own ticket state
- Compliance must not copy CI tables

## Shared kernel vs duplication

In BuildingBlocks / Platform:

- `UserId`, current user accessor
- numbering
- `IAuditHistory`
- `IAttachmentService`
- `ICommentService` / timeline
- `IWorkflowEngine` (scoped, not a BPM product)
- time (`IClock`)
- result/error types
- authorization attributes / policies

Modules **must not** reimplement these.

## Database

One database, multiple **schemas** recommended:

`id`, `org`, `plt`, `ntf`, `cmdb`, `sd`, `chg`, `rs`, `acc`, `ops`, `sec`, `gov`, `pol`, `cmp`, `evd`, `aud`, `bcm`, `tp`, `rpt`, `adm`

Cross-schema FKs are allowed for real relationships (e.g. `sd.Ticket.ConfigurationItemId` → `cmdb.ConfigurationItem`).

## Evolution

A module may become a separate service later **only** if it has a clear API, no cross-schema writes, and independent scale needs. Remote engine is already separate. Do not split the monolith preemptively.
