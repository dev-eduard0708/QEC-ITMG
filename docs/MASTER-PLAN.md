# QEC ITMG master plan

**Status:** DOCUMENTATION / ARCHITECTURE PHASE — no application features implemented.
**Organization:** Quality Education Company (QEC)
**Date:** 2026-09-02

This is the entry point for the program. Detailed design lives in linked documents.

---

## 1. Product summary

QEC ITMG is the internal platform for IT service management, remote support **governance**, asset/CMDB, operations, access, cybersecurity, IT governance, compliance, evidence, and audit. Daily work is designed to produce reusable evidence and an immutable trail from ticket to control.

Vision: [00-product/PRODUCT-VISION.md](00-product/PRODUCT-VISION.md) · Scope: [00-product/PRODUCT-SCOPE.md](00-product/PRODUCT-SCOPE.md) · Terms: [00-product/TERMINOLOGY.md](00-product/TERMINOLOGY.md)

## 2. Business goals

- One system of record for IT work and configuration
- Privileged remote access attributable in ITMG, not only in an engine
- Later GRC without duplicate applications/controls/evidence
- On-premises, maintainable, boring enterprise architecture
- Honest compliance reporting

Success: [00-product/SUCCESS-CRITERIA.md](00-product/SUCCESS-CRITERIA.md)

## 3. Architecture summary

Modular monolith: ASP.NET Core 10 host, React/Vite SPA, SQL Server, Hangfire, SignalR, Entra ID/AD SSO (BFF cookies), file blob abstraction, MeshCentral adapter for remote transport.

[01-architecture/SYSTEM-ARCHITECTURE.md](01-architecture/SYSTEM-ARCHITECTURE.md)

## 4. Module map

Identity · Organization · Platform · Notifications · Cmdb · ServiceDesk · ChangeManagement · RemoteSupport · AccessManagement · ItOperations · SecurityManagement · Governance · PolicyDocuments · Compliance · Evidence · AuditManagement · BusinessContinuity · ThirdParty · Reporting · Administration

Boundaries: [01-architecture/MODULAR-MONOLITH.md](01-architecture/MODULAR-MONOLITH.md) · Details: [03-modules/](03-modules/)

## 5. Domain relationship overview

```
Ticket → CI/Asset → RemoteSession → Incident → Problem → Change
     → Evidence → InternalControl → FrameworkRequirement → Audit
```

[02-domain/DOMAIN-RELATIONSHIPS.md](02-domain/DOMAIN-RELATIONSHIPS.md)

Event ≠ Incident ≠ Problem ≠ Change. Asset ≠ CI (overlap allowed).

## 6. Technology decisions

| Decision | Choice |
|----------|--------|
| UI | React + TS + Vite + Tailwind + shadcn |
| API | REST + SignalR |
| Backend | ASP.NET Core 10 modular monolith, EF Core, FluentValidation |
| Jobs | Hangfire |
| DB | SQL Server |
| Charts | Recharts |
| Remote | MeshCentral default, `IRemoteSupportEngine` |
| Authn | OIDC BFF + app RBAC |
| Files | SQL metadata + disk/SMB |

ADRs: [12-decisions/ADR-INDEX.md](12-decisions/ADR-INDEX.md)

## 7. Security principles

Least privilege, permission keys (not role name checks), resource-level authz, MFA for privileged, BFF cookies, IDOR tests, engine not a bypass, immutable security log, classification, upload scanning states, TLS, secrets out of source.

[04-security/SECURITY-ARCHITECTURE.md](04-security/SECURITY-ARCHITECTURE.md)

## 8. Compliance strategy

Internal-control-first. Frameworks are versioned data. Mapping ≠ assessment ≠ certification. COBIT, ISO/IEC 27001, COSO, NIST CSF, CIS, checklists, auditor questions are **different purposes**. Evidence reused across mappings.

[05-compliance/CONTROL-MODEL.md](05-compliance/CONTROL-MODEL.md)

## 9. MVP

Identity, org, RBAC, audit, platform services, CMDB/assets, service desk (SR + incidents), basic change, attended remote governance + adapter, notifications, attachments, comments, basic ops dashboard.

Not MVP: full GRC, JML automation, unattended at scale, AI, vendors, BCM.

[11-planning/MVP-DEFINITION.md](11-planning/MVP-DEFINITION.md)

## 10. Full phase roadmap

Phases **0–20** (21 phases). See [11-planning/IMPLEMENTATION-PHASES.md](11-planning/IMPLEMENTATION-PHASES.md).

| Phase | Name | In MVP? |
|-------|------|---------|
| 0 | Foundation | Yes |
| 1 | Identity/RBAC/audit | Yes |
| 2 | Platform services | Yes |
| 3 | CMDB/assets | Yes |
| 4 | Service desk | Yes |
| 5 | Incident extras / problem | Partial |
| 6 | Change | Yes |
| 7 | Remote support | Yes (attended) |
| 8 | Events / IT ops | No |
| 9 | Access / JML | No |
| 10 | Policy / documents | No |
| 11 | Governance / controls | No |
| 12 | Framework mapping | No |
| 13 | Evidence | No |
| 14 | Audit | No |
| 15 | Security mgmt | No |
| 16 | BCM | No |
| 17 | Vendors | No |
| 18 | Advanced reporting | Partial (widgets in P4) |
| 19 | Integrations | No |
| 20 | AI | No |

## 11. Dependency order

Identity → Platform → CMDB → Service desk → Change → Remote. Controls after CMDB+docs. Evidence after controls. Audit after evidence. AI last.

[11-planning/DEPENDENCY-MAP.md](11-planning/DEPENDENCY-MAP.md)

## 12. Major risks

Scope creep; remote engine bypass; duplicate registers; vanity compliance %; skipping history. [11-planning/RISK-REGISTER.md](11-planning/RISK-REGISTER.md)

## 13. Definition of Done

Authz + history + tests + docs for every package. [11-planning/DEFINITION-OF-DONE.md](11-planning/DEFINITION-OF-DONE.md)

## 14. Links to detailed documentation

| Area | Path |
|------|------|
| Product | [00-product/](00-product/) |
| Architecture | [01-architecture/](01-architecture/) |
| Domain | [02-domain/](02-domain/) |
| Modules | [03-modules/](03-modules/) |
| Security | [04-security/](04-security/) |
| Compliance | [05-compliance/](05-compliance/) |
| Data | [06-data/](06-data/) |
| API | [07-api/](07-api/) |
| UX | [08-ux/](08-ux/) |
| Testing | [09-testing/](09-testing/) |
| Operations | [10-operations/](10-operations/) |
| Planning | [11-planning/](11-planning/) |
| ADRs | [12-decisions/](12-decisions/) |

## 15. Recommended first coding phase

**Phase 0 / package P0-01 — Repository and solution foundation.**

Do not start with tickets or MeshCentral. Next after P0: Phase 1 identity and audit (security is not deferred).

---

## Non-functional requirements (summary)

| NFR | Approach |
|-----|----------|
| Security | 04-security |
| Maintainability | Modular monolith, boring stack |
| Observability | Serilog, health, Hangfire |
| Reliability | Transactions, retries, backups |
| Scalability | Vertical + optional extra host; not K8s first |
| Accessibility | WCAG 2.2 AA target |
| Responsiveness | Employee mobile; IT desktop |
| Backup/DR | 10-operations |
| Performance | Indexed lists, server reports |
| Retention | 06-data/RETENTION |
| Auditability | History from P1 |
| Localization | i18n keys, English first |
| Timezone | UTC store, org/user display |
| Concurrency | rowversion, numbering locks |
| Export | Permissioned, audited |

## Open decisions (QEC must confirm)

1. Entra ID tenant vs AD FS vs hybrid for production SSO
2. SQL Server edition/licensing
3. Confirm MeshCentral vs existing RustDesk standard
4. Malware scan product (Defender/ICAP)
5. Platform RTO/RPO numeric targets
6. Whether vendor users exist before Phase 17
7. COBIT/ISO content licensing for requirement text import
8. Unattended remote in a later MVP patch vs wait for P7-06 flag

## Engineering standards

C# / React / DB / Git: [01-architecture/SOLUTION-STRUCTURE.md](01-architecture/SOLUTION-STRUCTURE.md), [06-data/DATABASE-CONVENTIONS.md](06-data/DATABASE-CONVENTIONS.md)

## Out of scope reminder

[00-product/OUT-OF-SCOPE.md](00-product/OUT-OF-SCOPE.md)
