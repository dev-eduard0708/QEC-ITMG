# QEC IT Management & Governance Platform

**Short name:** QEC ITMG
**Organization:** Quality Education Company (QEC)
**Status:** FOUNDATION COMPLETE / ACTIVE DEVELOPMENT

QEC ITMG is the internal enterprise platform for IT service management, operations, remote support, asset/CMDB, cybersecurity, governance, compliance, evidence, and audit. Operational work is designed to produce structured history and reusable audit evidence rather than living in disconnected tools.

**Phase 0 (repository foundation) and Phase 1 (Identity) are complete.** Application source includes the modular-monolith host, Google OIDC BFF + break-glass, SQL RBAC, `/me` SPA session, admin users/roles UI, audit foundation, and identity seeds. Broader business modules (tickets, assets, change, GRC, remote support) are not implemented yet. Production deployment has not started.

## Current project status

| Item | Status |
|------|--------|
| Product vision and scope | Documented |
| Target architecture | Documented |
| Domain and data model | Documented (conceptual/logical) |
| Security and compliance design | Documented |
| Implementation roadmap | Documented |
| Phase 0 foundation | **Complete** |
| Phase 1 identity / org / audit | **Complete** |
| ASP.NET Core 10 modular monolith | Host + BuildingBlocks + Contracts + Identity / Organization / Platform |
| Frontend shell (React 19 + TypeScript + Vite) | Present (login, session, admin UI, theme + en/ar + RTL) |
| EF Core 10 + SQL Server foundation | Present (`QecItmg_Dev`; schemas `id` / `org` / `plt`) |
| Health / readiness + Serilog | Present |
| CI build/test pipeline | Present |
| Architecture boundary tests | Present |
| Business domain (tickets, assets, GRC, …) | **Not started** |
| Production deployment | **Not started** |

**Next step:** Phase 2 — Shared platform foundations (package **P2-08 Lookup admin**). See [docs/MASTER-PLAN.md](docs/MASTER-PLAN.md) and [docs/11-planning/IMPLEMENTATION-PHASES.md](docs/11-planning/IMPLEMENTATION-PHASES.md).

P0-07 (optional Docker Compose for SQL) is **deferred**: local SQL Express development already works.

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

## Architecture

Modular monolith: one deployable ASP.NET Core host, module boundaries by domain, shared kernel for identity, numbering, attachments, audit history, notifications, and workflow. React SPA will talk to REST APIs; SignalR is planned for live operational updates. SQL Server is the system of record.

Foundation code follows [docs/01-architecture/SYSTEM-ARCHITECTURE.md](docs/01-architecture/SYSTEM-ARCHITECTURE.md). Most business modules remain documentation-only until their phase.

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

**Phase 0 complete.** The repository contains:

- ASP.NET Core 10 modular monolith (`Host`, `BuildingBlocks`, `Contracts`, Identity / Organization / Platform foundations)
- React 19 + TypeScript + Vite frontend shell with light/dark/system theme and English/Arabic + RTL
- EF Core 10 + SQL Server foundation and `QecItmg_Dev` foundation migrations (no business entities yet)
- `/health/live` and SQL-aware `/health/ready`, Serilog
- GitHub Actions CI (build/test) and NetArchTest module-boundary tests

Do **not** treat planned module catalogs (service desk, CMDB, GRC, and so on) as implemented packages. Production deployment has not started.

**Next coding phase:** Phase 1 — Identity / RBAC (first package **P1-01** User / Role / Permission domain). See [docs/11-planning/IMPLEMENTATION-PHASES.md](docs/11-planning/IMPLEMENTATION-PHASES.md).
