# QEC IT Management & Governance Platform

**Short name:** QEC ITMG
**Organization:** Quality Education Company (QEC)
**Status:** DOCUMENTATION / ARCHITECTURE PHASE

QEC ITMG is the planned internal enterprise platform for IT service management, operations, remote support, asset/CMDB, cybersecurity, governance, compliance, evidence, and audit. Operational work is designed to produce structured history and reusable audit evidence rather than living in disconnected tools.

This repository does **not** yet contain application code. Features listed below are **planned**, not implemented.

## Current project status

| Item | Status |
|------|--------|
| Product vision and scope | Documented |
| Target architecture | Documented |
| Domain and data model | Documented (conceptual/logical) |
| Security and compliance design | Documented |
| Implementation roadmap | Documented |
| Application source (ASP.NET Core / React) | **Not started** |
| Database migrations | **Not started** |
| APIs | **Not started** |
| Production deployment | **Not started** |

**Next step after documentation review:** Phase 0 — repository foundation and engineering standards. See [docs/MASTER-PLAN.md](docs/MASTER-PLAN.md) and [docs/11-planning/IMPLEMENTATION-PHASES.md](docs/11-planning/IMPLEMENTATION-PHASES.md).

## Purpose

- Give QEC a single internal system of record for IT work, assets, change, access, and control evidence.
- Connect tickets, remote sessions, configuration items, incidents, problems, changes, approvals, evidence, and audits.
- Support later governance and compliance programs (COBIT, ISO/IEC 27001, NIST CSF, CIS, COSO, internal checklists, auditor questionnaires) without duplicating controls or evidence.
- Remain deployable on-premises first, with clear boundaries so selected components can move later.

## Planned capabilities (not implemented)

- Service desk: tickets, service requests, incidents, problems, SLA, knowledge base
- Remote support integration (authorization and audit owned by QEC ITMG; transport via a proven engine)
- Asset management and CMDB
- Change, event, and IT operations
- Access management (joiner / mover / leaver, reviews, privileged accounts)
- Governance, policy, compliance, evidence, audit
- Security operations (vulnerabilities, security incidents, risk, exceptions)
- Business continuity / DR, vendor management
- Reporting, notifications, automation; AI assistance as a later phase

## Proposed technology stack

| Layer | Choice |
|-------|--------|
| Frontend | React, TypeScript, Vite, Tailwind CSS, shadcn/ui, React Router, TanStack Query, TanStack Table, React Hook Form, Zod, SignalR client, Recharts |
| Backend | ASP.NET Core 10, C#, REST, SignalR, modular monolith, EF Core, FluentValidation, Hangfire, OpenAPI, health checks, structured logging |
| Database | Microsoft SQL Server |
| Identity | Microsoft Entra ID / Active Directory SSO, application RBAC, MFA for privileged users |
| Deployment | Internal/on-premises first, HTTPS, reverse proxy, Dev/Staging/Production |

Rationale is recorded in [docs/12-decisions/ADR-INDEX.md](docs/12-decisions/ADR-INDEX.md).

## Architecture (planned)

Modular monolith: one deployable ASP.NET Core host, module boundaries by domain, shared kernel for identity, numbering, attachments, audit history, notifications, and workflow. React SPA talks to REST APIs; SignalR is used for live operational updates. SQL Server is the system of record.

See [docs/01-architecture/SYSTEM-ARCHITECTURE.md](docs/01-architecture/SYSTEM-ARCHITECTURE.md).

## Documentation

**Start here:** [docs/MASTER-PLAN.md](docs/MASTER-PLAN.md)

| Area | Path |
|------|------|
| Product | [docs/00-product/](docs/00-product/) |
| Architecture | [docs/01-architecture/](docs/01-architecture/) |
| Domain | [docs/02-domain/](docs/02-domain/) |
| Modules | [docs/03-modules/](docs/03-modules/) |
| Security | [docs/04-security/](docs/04-security/) |
| Compliance | [docs/05-compliance/](docs/05-compliance/) |
| Data | [docs/06-data/](docs/06-data/) |
| API | [docs/07-api/](docs/07-api/) |
| UX | [docs/08-ux/](docs/08-ux/) |
| Testing | [docs/09-testing/](docs/09-testing/) |
| Operations | [docs/10-operations/](docs/10-operations/) |
| Planning | [docs/11-planning/](docs/11-planning/) |
| ADRs | [docs/12-decisions/](docs/12-decisions/) |

## Implementation status

No production backend, frontend, APIs, or database schema have been implemented. Do not treat module names in documentation as existing code packages.

When implementation begins, follow Phase 0 in [docs/11-planning/IMPLEMENTATION-PHASES.md](docs/11-planning/IMPLEMENTATION-PHASES.md). The first coding package is **P0-01 Repository and solution foundation**.
