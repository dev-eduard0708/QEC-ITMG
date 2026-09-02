# Domain model

Related: [DOMAIN-RELATIONSHIPS.md](DOMAIN-RELATIONSHIPS.md) · [SHARED-KERNEL.md](SHARED-KERNEL.md) · [../06-data/DATA-MODEL-OVERVIEW.md](../06-data/DATA-MODEL-OVERVIEW.md) · [../00-product/TERMINOLOGY.md](../00-product/TERMINOLOGY.md)

## Modeling rules

1. Aggregates have a GUID id and a business number where humans search them.
2. Cross-aggregate references use ids; do not mutate another aggregate’s internals.
3. Shared kernel types are identifiers, clocks, money-less value objects, classification, and platform services — not a god `Entity` table.
4. Prefer real FKs. Polymorphic links only as listed in [../01-architecture/DATABASE-ARCHITECTURE.md](../01-architecture/DATABASE-ARCHITECTURE.md).

## Aggregate roots (canonical)

### Identity & organization

| Root | Responsibility |
|------|----------------|
| User | Platform identity, status, directory object id |
| Role | Named permission set |
| Permission | Stable string key |
| Department | Org unit |
| Location | Physical/logical site |
| OrganizationalUnit | Optional finer org chart node |

### Platform

| Root | Responsibility |
|------|----------------|
| NumberSequence | Concurrency-safe business numbers |
| Attachment | File metadata |
| CommentThread | Comments on a parent |
| WorkflowDefinition / WorkflowInstance | Scoped state machine |
| BusinessAuditRecord | Field/state history |
| SecurityAuditEvent | Security log |

### CMDB / assets

| Root | Responsibility |
|------|----------------|
| ConfigurationItem | Operational CI |
| CiRelationship | Typed edge (may be entity inside CI module, uniqueness on pair+type) |
| Asset | Financial/custody; optional `ConfigurationItemId` |
| BusinessService | Service offered to business |
| CiType | Type definition (data, not code-only) |

### Service desk

| Root | Responsibility |
|------|----------------|
| Ticket | Incident or service request |
| SlaPolicy / SlaClock | Timing |
| KnowledgeArticle | KB |
| Problem | Root cause record |

Incident is **not** a separate root: it is `Ticket` with `TicketType = Incident` plus incident fields. Security incident is the same ticket with `SecurityClassification` and permission filter. Service request is `TicketType = ServiceRequest`.

### Change

| Root | Responsibility |
|------|----------------|
| ChangeRequest | Standard/normal/emergency change |

### Remote support

| Root | Responsibility |
|------|----------------|
| RemoteSessionRequest | Ask / consent |
| RemoteSession | Actual session bound to request |

### Access

| Root | Responsibility |
|------|----------------|
| AccessCase | JML or ad-hoc access request |
| AccessReview | Campaign / review |
| PrivilegedAccount | Named privileged identity tracking |
| ServiceAccountRecord | Non-human account tracking |

### Operations & security

| Root | Responsibility |
|------|----------------|
| OperationalEvent | Normalized event |
| BackupJob / RestoreTest | Ops evidence sources |
| CertificateRecord | Cert inventory |
| Vulnerability | Finding from scanner or manual |
| Risk | Risk register item |
| PolicyException | Time-bounded exception (also used by compliance) |

### Governance / compliance / evidence / audit

| Root | Responsibility |
|------|----------------|
| InternalControl | QEC control |
| Framework / FrameworkVersion / FrameworkRequirement | Reference data |
| ControlMapping | Control ↔ requirement |
| ControlAssessment | Test result for a period |
| ManagedDocument / PolicyVersion | Policies and documents |
| Evidence | Reusable evidence |
| AuditEngagement | Internal/external audit |
| Finding | Audit/assessment finding |
| CorrectiveAction | Remediation |

### BCM / third party / notifications

| Root | Responsibility |
|------|----------------|
| BiaRecord / DisasterRecoveryPlan / DrTest | Continuity |
| Vendor / Contract / VendorAssessment | Third parties |
| Notification | User notification |
| NotificationTemplate | Channel template |

## Ticket vs Change vs Event vs Problem

| Concept | Aggregate | Created when |
|---------|-----------|--------------|
| Event | OperationalEvent | Something was observed |
| Ticket (Incident) | Ticket | Service impact / interruption |
| Ticket (Service request) | Ticket | User wants a standard service |
| Problem | Problem | Cause analysis spanning incidents |
| Change | ChangeRequest | Controlled modification |

An event **may** spawn an incident (explicit action or rule). Multiple incidents **may** link to one problem. A problem **may** require a change. A change **must** list affected CIs.

## Asset vs CI

See [../03-modules/ASSET-CMDB.md](../03-modules/ASSET-CMDB.md). Domain allows:

- CI without asset (e.g. virtual NIC, interface)
- Asset without CI (e.g. spare license lot)
- Both (laptop)

## Control vs requirement vs evidence

InternalControl is executed and owned. FrameworkRequirement is citation. Evidence proves control operation for a period and may be linked to many mappings.
