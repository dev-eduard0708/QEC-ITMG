# Product scope

Related: [PRODUCT-VISION.md](PRODUCT-VISION.md) · [OUT-OF-SCOPE.md](OUT-OF-SCOPE.md) · [../11-planning/MVP-DEFINITION.md](../11-planning/MVP-DEFINITION.md) · [../03-modules/](../03-modules/)

Scope is described at three layers: **platform**, **MVP**, and **full product**. Implementation order is in [../11-planning/IMPLEMENTATION-PHASES.md](../11-planning/IMPLEMENTATION-PHASES.md).

## In scope — full product (planned)

| Domain | Scope summary | Module doc |
|--------|---------------|------------|
| Identity & administration | Users, roles, permissions, departments, locations, settings | [ADMINISTRATION.md](../03-modules/ADMINISTRATION.md) |
| Service desk | Tickets, service requests, incidents, problems, SLA, knowledge | [SERVICE-DESK.md](../03-modules/SERVICE-DESK.md) |
| Remote support | Retained (attended/unattended governance + engine); **lower near-term priority** than P5→P6→P8→P9→P11–P14 | [REMOTE-SUPPORT.md](../03-modules/REMOTE-SUPPORT.md) |
| Assets / CMDB | External Asset Management = physical lifecycle SoR; ITMG Assets = correlation; ITMG owns operational CI/service relationships | [ASSET-CMDB.md](../03-modules/ASSET-CMDB.md) |
| Change | Standard / normal / emergency changes | [CHANGE-MANAGEMENT.md](../03-modules/CHANGE-MANAGEMENT.md) |
| Events / IT ops | Events, monitoring, backups, patches, jobs, certs, capacity | [EVENT-MANAGEMENT.md](../03-modules/EVENT-MANAGEMENT.md), [IT-OPERATIONS.md](../03-modules/IT-OPERATIONS.md) |
| Access | JML, privileged and service accounts, reviews, SoD | [ACCESS-MANAGEMENT.md](../03-modules/ACCESS-MANAGEMENT.md) |
| Governance | Org, registers, diagrams, interfaces, control framework hosting; COBIT governance/control mapping | [GOVERNANCE.md](../03-modules/GOVERNANCE.md) |
| Policy / documents | Policies, procedures, standards, versioning, acknowledgements | [POLICY-MANAGEMENT.md](../03-modules/POLICY-MANAGEMENT.md), [DOCUMENT-MANAGEMENT.md](../03-modules/DOCUMENT-MANAGEMENT.md) |
| Security | Vulnerabilities, pentests, security incidents, DLP metadata, awareness, risk, exceptions | [SECURITY-MANAGEMENT.md](../03-modules/SECURITY-MANAGEMENT.md) |
| BCM | BIA, BCP, IT DR, RTO/RPO, tests, SPOFs | [BUSINESS-CONTINUITY.md](../03-modules/BUSINESS-CONTINUITY.md) |
| Third parties | Vendors, contracts, vendor access, assessments | [THIRD-PARTY-MANAGEMENT.md](../03-modules/THIRD-PARTY-MANAGEMENT.md) |
| Compliance | Frameworks, mappings, assessments, calendar | [COMPLIANCE.md](../03-modules/COMPLIANCE.md) |
| Evidence | Reusable evidence register and links | [EVIDENCE-LIBRARY.md](../03-modules/EVIDENCE-LIBRARY.md) |
| Audit | Internal/external audits, findings, CAPA, export; ISA 315–oriented IT audit profile | [AUDIT-MANAGEMENT.md](../03-modules/AUDIT-MANAGEMENT.md) |
| Notifications | In-app and email, templates, retries | [NOTIFICATIONS.md](../03-modules/NOTIFICATIONS.md) |
| Reporting | Server-side metrics and dashboards | [REPORTING.md](../03-modules/REPORTING.md) |
| Integrations | Adapters for AD/Entra, mail, monitoring, remote engine, later systems | [../01-architecture/INTEGRATION-ARCHITECTURE.md](../01-architecture/INTEGRATION-ARCHITECTURE.md) |
| AI (future, last) | Assistive, authorization-bound | [../11-planning/MASTER-ROADMAP.md](../11-planning/MASTER-ROADMAP.md) |

## In scope — MVP

See [../11-planning/MVP-DEFINITION.md](../11-planning/MVP-DEFINITION.md). In short: identity, organization, service desk (tickets, service requests, incidents), basic CMDB/assets and assignment, ticket–CI linking, basic change, remote-support **governance** plus engine integration for attended sessions, attachments, comments/timeline, notifications, immutable audit history, basic operational dashboard.

## Explicitly later than MVP (still in full product)

- Full problem management practice (MVP may allow linking a problem record; the full process is later)
- Unattended remote as a general capability (MVP: attended; unattended only if a tightly scoped admin path is ready)
- Full JML automation against AD
- Control library, framework mapping, evidence library, audit module
- Vulnerability ingestion, BCM, vendor GRC
- Advanced executive/compliance dashboards
- Teams/SMS/push, SIEM, AI

## Scope rules

1. **No custom remote desktop protocol.** Integrate a proven engine. [ADR-0008](../12-decisions/ADR-0008-remote-support-integration.md).
2. **No claim of certification.** The platform stores and reports control work; it does not “make QEC ISO 27001 certified.”
3. **No second CMDB** inside DR, security, or compliance modules.
4. **Framework content is data**, not C# enums of COBIT processes.
5. **English UI first**, localization-ready strings and resource keys from the start.

## Organizational scope

Designed for **internal QEC use**. Multi-tenant SaaS, customer-facing ITSM, and public internet exposure of the management plane are out of product intent. Vendor users may exist later as restricted identities for third-party access tracking, not as a public portal MVP.
