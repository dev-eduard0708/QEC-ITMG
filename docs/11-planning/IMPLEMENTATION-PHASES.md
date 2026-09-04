# Implementation phases

Related: [MASTER-ROADMAP.md](MASTER-ROADMAP.md) � [MVP-DEFINITION.md](MVP-DEFINITION.md) � [DEFINITION-OF-DONE.md](DEFINITION-OF-DONE.md)

Phases are ordered by **dependency**, not by sidebar menus. Each package is a later Cursor implementation task.

**Phase count: 21 (Phase 0 through Phase 20).**

Global out-of-scope for every phase: custom remote protocol, microservices, Kafka, skipping authz/history.

---

## Phase 0 ? Architecture, repository foundation, engineering standards

**Status:** Complete (P0-07 Docker Compose for SQL optionally deferred; local SQL Express in use).

**Objective:** Empty repo becomes a buildable modular-monolith skeleton with CI hooks and no business features.

**Prerequisites:** Documentation accepted (this set).

**Scope:** Solution layout per [../01-architecture/SOLUTION-STRUCTURE.md](../01-architecture/SOLUTION-STRUCTURE.md); EditorConfig; analyzers; frontend Vite shell; health endpoint; `.gitignore`; directory.Build.props; architecture test project empty rules.

**Out of scope:** Tickets, SSO completion (stub ok), MeshCentral.

### Packages

| ID | Work |
|----|------|
| P0-01 | Repository and solution foundation (first coding package) |
| P0-02 | Host, DI module interface, health checks, Serilog |
| P0-03 | Frontend Vite/React/Tailwind/shadcn shell, routing stub |
| P0-04 | EF conventions, SQL connection, migrate empty |
| P0-05 | CI pipeline (build/test) |
| P0-06 | Architecture tests skeleton |
| P0-07 | Docker compose optional SQL for dev (**deferred** ? SQL Express already works locally) |
| P0-08 | Phase 0 closeout: README / status pointers reflect foundation complete |

**Backend:** Host, BuildingBlocks empty types (`IClock`).
**Frontend:** App shell, design tokens.
**Database:** Empty or `plt` placeholder.
**Security:** HTTPS dev certs; no anonymous business API.
**Audit:** N/A yet.
**Tests:** Solution builds; one architecture test (Host references modules correctly).
**Dependencies:** None.
**Acceptance / exit:** `dotnet build` + `npm run build` succeed; no feature claims in README.

---

## Phase 1 ? Identity, organization, users, roles, permissions, audit foundation

**Status:** Complete (P1-01 through P1-10).

**Objective:** Real users, RBAC, org lookups, immutable security + business audit for identity changes.

**Prerequisites:** P0.

**Out of scope:** Tickets (later phases).

### Packages

| ID | Work | Status |
|----|------|--------|
| P1-01 | User, Role, Permission entities | Done |
| P1-02 | OIDC BFF cookie auth (Google primary) | Done |
| P1-03 | Permission policies | Done |
| P1-04 | Departments, locations | Done |
| P1-05 | Admin UI users/roles | Done |
| P1-06 | BusinessAuditRecord + SecurityAuditEvent | Done |
| P1-06B | Google OIDC pivot + Mailpit SMTP config | Done |
| P1-07 | Break-glass local emergency login | Done |
| P1-08 | Authz tests | Done |
| P1-09 | `/me` endpoint and SPA session | Done |
| P1-10 | Seed Employee + Platform Administrator | Done |

**Security:** Google Workspace MFA for privileged accounts; app step-up placeholder claim.
**Audit:** Role assignment history required.
**Acceptance:** Cannot call admin API as Employee; history row on role change; `/me` + SPA session; idempotent identity seed.

**Next:** Phase 2 / **P2-01 NumberSequence concurrency**.

---

## Phase 2 ? Shared platform foundations

**Objective:** Numbering, attachments, comments/timeline, workflow, notifications, lookups.

**Prerequisites:** P1.

**Out of scope:** Ticket types.

### Packages

| ID | Work |
|----|------|
| P2-01 | NumberSequence concurrency (Done) |
| P2-02 | Attachment metadata + disk storage (Done) |
| P2-03 | Malware scan state machine (Done) |
| P2-04 | Comments visibility (Done) |
| P2-05 | Workflow engine (data-driven states) (Done) |
| P2-06 | Notification entity + in-app (Done) |
| P2-07 | Email channel Hangfire (Done) |
| P2-08 | Lookup admin (Done) |
| P2-09 | Tests numbering races (Done) |
| P2-10 | SPA shared components (table, timeline) (Done) |

**Phase 2 status: COMPLETE.**

---

## Phase 3 ? Asset management / CMDB foundation

**Objective:** Operational CMDB foundation in ITMG, with Asset records as a compatibility/correlation layer.

**Source-of-truth model (unchanged by Phase 3 completion):**

- **External Asset Management** = authoritative physical asset lifecycle source of truth
- **QEC ITMG** = operational CI / service relationship source, plus existing Asset compatibility/correlation records

**Prerequisites:** P2.

**Out of scope:** Discovery integrations, full relationship graph UX (simple list + typed links ok).

### Packages

| ID | Work |
|----|------|
| P3-01 | CiType, ConfigurationItem (Done) |
| P3-02 | Relationships (Done) |
| P3-03 | Asset + assignment + custody (Done) |
| P3-04 | BusinessService stub (Done) |
| P3-05 | APIs + authz classification (Done) |
| P3-06 | IT UI list/detail (Done) |
| P3-07 | Employee My Equipment (Done) |
| P3-08 | History on assignment (Done) |
| P3-09 | Tests (Done) |
| P3-10 | Seed types (laptop, server, application) (Done) |

**Phase 3 is complete.**

---

## Phase 4 ? Service desk / support tickets / service requests

**Objective:** Daily ITSM for SR and incidents (incident = ticket type).

**Prerequisites:** P3 (link CI). P2 SLA needs Hangfire.

**Out of scope:** Full problem, major-incident war room, email-in.

### Packages

| ID | Work |
|----|------|
| P4-01 | Ticket domain (Done) |
| P4-02 | Ticket API (Done) |
| P4-03 | Assignment / queues (Done) |
| P4-04 | SLA engine (Done) |
| P4-05 | Ticket UI employee + IT | Done |
| P4-06 | Attachments on ticket | Done |
| P4-07 | Timeline | Done |
| P4-08 | Notifications assign/SLA | Done |
| P4-09 | Tests including IDOR | Done |
| P4-10 | E2E employee request | Done |
| P4-11 | KB published read + simple manage | Done |
| P4-12 | Operational dashboard widgets | Done |

---

## Phase 5 ? Incident and problem management

**Objective:** Incident specialization (major flag, security classification) and Problem aggregate.

**Prerequisites:** P4.

**MVP note:** Security classification + major flag should not wait if P4 shipped without them ? include in P4 if possible; P5 completes problem.

### Packages

| ID | Work |
|----|------|
| P5-01 | Incident fields, security permission | Done |
| P5-02 | Promote from event (stub until P8) | Done |
| P5-03 | Problem domain/API/UI | Done |
| P5-04 | Link incidents | Done |
| P5-05 | Recurring metrics | Done |
| P5-06 | Tests security IDOR | Deferred — dedicated testing day |
| P5-07 | Known error flag | Done |

**Out of scope:** Full PIR methodology UI.

**Feature status:** FEATURE COMPLETE (VALIDATION/HARDENING DEFERRED).

**Next:** P6 Change Management.

**Note:** P7 Remote Support remains retained but lower priority. Near-term execution priority: **P5 ? P6 ? P8 ? P9 ? P11?P14**.

---

## Phase 6 ? Change management

**Objective:** Standard/normal/emergency with SoD approvals and CI links.

**Prerequisites:** P3, P2 workflow. P4 for optional ticket link.

### Packages

| ID | Work |
|----|------|
| P6-01 | Change domain |
| P6-02 | API + CI links |
| P6-03 | Approval SoD |
| P6-04 | Implementation/validation/PIR states |
| P6-05 | UI |
| P6-06 | Notifications approval |
| P6-07 | Tests SoD |
| P6-08 | Standard change catalog (optional) |
| P6-09 | Emergency retrospective path |
| P6-10 | History |

---

## Phase 7 — Remote support integration

**Objective:** ITMG-owned attended sessions; adapter to MeshCentral; no engine bypass.

**Priority note:** Retained in roadmap but **lower near-term priority** than P6 → P8 → P9 → P11–P14.

**Prerequisites:** P1, P3, P4 (ticket). P6 recommended before unattended.

### Packages

| ID | Work |
|----|------|
| P7-00 | Engine spike / lab MeshCentral |
| P7-01 | RemoteSessionRequest domain |
| P7-02 | Consent API/UI |
| P7-03 | `IRemoteSupportEngine` + MeshCentral adapter |
| P7-04 | Active/history/audit log UI |
| P7-05 | Webhook/poll end session |
| P7-06 | Unattended flag **off** in prod MVP |
| P7-07 | Security tests (no start without authz) |
| P7-08 | Degraded mode |
| P7-09 | Notifications |
| P7-10 | E2E with mock engine |

**Out of scope:** Custom protocol; Guacamole (later adapter).

---

## Phase 8 ? Events, monitoring, IT operations

**Prerequisites:** P3. P4 for promote-to-incident.

### Packages

P8-01 Event ingest/dedup; P8-02 Acknowledge/promote; P8-03 Backup/restore test records; P8-04 Certificates expiry notify; P8-05 Patch metadata; P8-06 Jobs metadata; P8-07 Retention; P8-08 UI; P8-09 Permissions; P8-10 Tests.

**Out of scope:** SIEM replacement, raw syslog.

---

## Phase 9 ? Access management / JML

**Prerequisites:** P1, P3 (apps as CI), P2 workflow. P4 optional work orders.

### Packages

P9-01 AccessCase; P9-02 Joiner; P9-03 Mover; P9-04 Leaver checklist; P9-05 Access request; P9-06 Reviews; P9-07 Privileged/service accounts; P9-08 SoD rules; P9-09 Evidence promote hook (manual); P9-10 Tests.

**Out of scope:** Full AD automation (interface only / checklist).

---

## Phase 10 ? Policy and document management

**Prerequisites:** P2 files, P1 users.

### Packages

P10-01 ManagedDocument versions; P10-02 Policy UX; P10-03 Approvals; P10-04 Acknowledgements; P10-05 Review notifications; P10-06 Diagram type; P10-07 Tests.

---

## Phase 11 ? Governance and control library

**Prerequisites:** P3 for registers-as-views; P10 optional policy links.

### Packages

P11-01 Org chart; P11-02 Register views; P11-03 InternalControl CRUD; P11-04 Owners/frequency; P11-05 TestProcedure; P11-06 UI; P11-07 Permissions; P11-08 Tests.

**Out of scope:** Framework content packs (P12).

---

## Phase 12 ? Compliance framework mapping

**Prerequisites:** P11.

### Packages

P12-01 Framework version requirement import model; P12-02 Mapping UI; P12-03 Coverage APIs (honest); P12-04 Assessment; P12-05 Calendar; P12-06 Seed **structure** not full COBIT text (licensing!); P12-07 Tests; P12-08 No vanity %.

Content packs: load licensed/public text as **data files**, not code.

---

## Phase 13 ? Evidence library

**Prerequisites:** P11, P2 files.

### Packages

P13-01 Evidence entity; P13-02 Links; P13-03 Validity job; P13-04 Accept workflow; P13-05 Export permission (may wait P14); P13-06 UI; P13-07 Tests classification; P13-08 Promote from change/restore if those exist.

---

## Phase 14 ? Audit management

**Prerequisites:** P13.

### Packages

P14-01 Engagement; P14-02 Questions; P14-03 Findings; P14-04 CAPA; P14-05 Evidence requests; P14-06 Export pack + audit log; P14-07 Auditor role; P14-08 Tests.

---

## Phase 15 ? Security management

**Prerequisites:** P3, P4 (security tickets), P11 optional risk-control link.

### Packages

P15-01 Vulnerability; P15-02 Remediation link change/ticket; P15-03 Risk register; P15-04 Exceptions; P15-05 Pentest record; P15-06 Awareness completions; P15-07 Dashboard; P15-08 Tests.

Scanner ingest: stub adapter.

---

## Phase 16 ? Business continuity

**Prerequisites:** P3 BusinessService RTO/RPO.

### Packages

P16-01 BIA; P16-02 Plans; P16-03 Procedures; P16-04 DR tests + evidence; P16-05 SPOF flag; P16-06 Reports; P16-07 Tests.

---

## Phase 17 ? Vendor / third-party

**Prerequisites:** P3 vendor FK may already exist; this fills Vendor aggregate.

### Packages

P17-01 Vendor/contract; P17-02 Assessments; P17-03 Expiry notify; P17-04 Vendor access link; P17-05 UI; P17-06 Tests.

---

## Phase 18 ? Advanced reporting / executive dashboards

**Prerequisites:** Data from prior phases. MVP used P4-12 widgets.

### Packages

P18-01 Report API package; P18-02 Snapshots Hangfire; P18-03 Exec dashboard; P18-04 Compliance honest tiles; P18-05 Export CSV audited; P18-06 Permissions per report; P18-07 Tests.

---

## Phase 19 ? Integrations / automation

**Prerequisites:** Adapters behind interfaces already. This wires real systems.

### Packages

P19-01 Directory sync/JML; P19-02 Mail/M365; P19-03 Veeam/events; P19-04 vCenter/Hyper-V enrich; P19-05 Vuln scanner; P19-06 SIEM outbound; P19-07 Webhook hardening; P19-08 Tests/secrets.

**Out of scope:** Building those vendors? products.

---

## Phase 20 ? AI assistance

**Prerequisites:** Stable APIs + RBAC. Local/on-prem model optional.

### Packages

P20-01 Tool-calling gateway **as user**; P20-02 Classification suggest; P20-03 KB suggest; P20-04 Summaries; P20-05 NL query restricted to permitted reports; P20-06 Redaction; P20-07 Prompt-injection tests; P20-08 Never unattended remote via model.

**Out of scope:** Autopilot changes to production.

---

## Suggested next coding task

**Phase 0 is complete. Phase 1 (Identity) is complete. Phase 2 (shared platform foundations) is complete. Phase 3 (Asset management / CMDB foundation) is complete. Phase 4 (Service desk) is COMPLETE (P4-01..P4-12).** Phase 5 FEATURE COMPLETE (P5-01..P5-05, P5-07 Done; P5-06 deferred to testing day). Next: **P6 Change Management**.
