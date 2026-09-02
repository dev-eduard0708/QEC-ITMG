# Terminology

This glossary is authoritative. Module documents must use these names. Database entity names are in [../06-data/ENTITY-CATALOG.md](../06-data/ENTITY-CATALOG.md).

## Product and organization

| Term | Meaning |
|------|---------|
| QEC | Quality Education Company |
| QEC ITMG | This platform |
| Platform | The QEC ITMG application and its datastore |
| Engine (remote) | External remote-support product (e.g. MeshCentral) that transports screen/input |

## People and identity

| Term | Meaning |
|------|---------|
| User | Authenticated platform identity (usually an Entra ID / AD account) |
| Employee | QEC worker using the employee experience; not a separate login type |
| Technician | User with IT operational permissions |
| Privileged user | User with high-impact permissions (unattended remote, role admin, evidence export, etc.) |
| Break-glass account | Local emergency admin, tightly controlled, fully audited |
| Service account | Non-human technical identity used by systems |
| Role | Named bundle of permissions |
| Permission | Atomic authorization key, e.g. `change.approve` |
| Resource-level authorization | Permission plus ownership/queue/CI/confidentiality checks |

## Service desk / ITSM

| Term | Meaning |
|------|---------|
| Event | Observable occurrence (e.g. backup failed). Not automatically an incident |
| Incident | Unplanned interruption or degradation of a service or CI |
| Security incident | Incident with security classification and restricted permissions/workflow |
| Service request | Normal, often catalog-based user request (access, equipment, information) |
| Support ticket | Employee-facing work item; classified as incident or service request (or inquiry) |
| Ticket | Canonical service-desk work record (incident or service request) |
| Problem | Underlying cause of one or more incidents |
| Change | Controlled modification to a service, CI, or process |
| Standard change | Pre-authorized, low risk, repeatable |
| Normal change | Assessed and approved through the full path |
| Emergency change | Expedited; remains auditable; requires retrospective review |
| Knowledge article | Published reusable guidance |
| SLA | Timed commitment (response/resolution) against a ticket or service |
| Queue | Work pool (often mapped to a support group) |
| Watcher | User notified of ticket updates who may not be assignee |

Do not use “ticket” for changes, problems, audits, or remote sessions. Those are separate aggregates that may **link** to tickets.

## Assets and configuration

| Term | Meaning |
|------|---------|
| Asset | Financial / ownership / lifecycle record (purchase, warranty, custody, disposal) |
| Configuration item (CI) | Operational item in the CMDB (service dependency, relationships, criticality) |
| CMDB | Configuration management database — the CI graph |
| Business service | Service offered to the business, mapped to CIs |
| Application | Software system treated primarily as a CI (and optionally a software asset) |
| Relationship | Directed, typed link between CIs (hosted-on, depends-on, connects-to, etc.) |
| Criticality | Business impact class of a CI or service (e.g. Critical / High / Medium / Low) |

A laptop is typically **both** asset and CI. A license is typically an **asset**. A network circuit is typically a **CI** with vendor/contract links. See [../03-modules/ASSET-CMDB.md](../03-modules/ASSET-CMDB.md).

## Remote support

| Term | Meaning |
|------|---------|
| Attended session | Remote access requiring end-user consent at request time |
| Unattended session | Remote access to a managed system without interactive user consent, policy-permitted only |
| Session request | ITMG record asking to start remote access |
| Consent | Explicit allow/decline by the device user (attended) |
| Remote session | Time-bounded access instance with start/end and audit fields |

## Governance, risk, compliance

| Term | Meaning |
|------|---------|
| Framework | Named external or internal reference (COBIT, ISO/IEC 27001, …) with versions |
| Framework requirement | A clause, practice, or question inside a framework version |
| Internal control | QEC’s actual control (the source of truth) |
| Control mapping | Link from an internal control to one or more framework requirements |
| Assessment | Test of a control (or requirement) for a period |
| Evidence | Reusable artifact proving control operation for a period |
| Finding | Documented gap from audit or assessment |
| Corrective action | Tracked remediation of a finding or failed assessment |
| Exception | Formal, time-bounded acceptance of a control gap or policy deviation |
| Policy | Binding management statement (a managed document type) |
| Acknowledgement | Record that a user attested to a policy version |

Frameworks are **not equivalent**. See [../05-compliance/FRAMEWORK-MAPPING.md](../05-compliance/FRAMEWORK-MAPPING.md).

## Compliance scoring (use carefully)

| Term | Meaning |
|------|---------|
| Mapped coverage | Requirements that have at least one mapped internal control |
| Assessed coverage | Mapped controls (or requirements) with a current assessment |
| Compliant / partially / non-compliant / N/A | Assessment results — not automatic from mapping |
| Evidence missing / expired | Evidence state independent of assessment result |
| Single compliance percentage | Forbidden as an unexplained headline metric |

## Audit and history

| Term | Meaning |
|------|---------|
| Business audit history | Domain-level before/after record of significant field/state changes |
| Security audit log | Security-relevant events (authn, authz failures, exports, privileged actions) |
| Timeline | User-visible chronological activity on a record |
| Attachment | File stored via the central file service, referenced by metadata |

## Time and records

| Term | Meaning |
|------|---------|
| UTC | Storage and API canonical clock |
| Soft delete | Record marked deleted, retained, hidden from normal lists |
| Archive | Retention-state move; still retained per policy |
| Hard delete | Physical removal — not available in normal UI for compliance records |

## Identifiers

Human-readable numbers (e.g. `INC-2026-000001`) are **business numbers**, unique per type. Technical primary keys are GUIDs. See [../02-domain/IDENTIFIERS-AND-NUMBERING.md](../02-domain/IDENTIFIERS-AND-NUMBERING.md).
