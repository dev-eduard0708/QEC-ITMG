# Security management

Related: [INCIDENT-MANAGEMENT.md](INCIDENT-MANAGEMENT.md) · [../04-security/SECURITY-ARCHITECTURE.md](../04-security/SECURITY-ARCHITECTURE.md) · [../05-compliance/CYBERSECURITY-CHECKLIST-STRATEGY.md](../05-compliance/CYBERSECURITY-CHECKLIST-STRATEGY.md) · [../05-compliance/ISA-315-AUDIT-PROFILE.md](../05-compliance/ISA-315-AUDIT-PROFILE.md)

## Purpose

Operational cybersecurity records **on top of CMDB and tickets**, not a parallel asset list.

## Capabilities

| Area | Model |
|------|--------|
| Security dashboard | Server metrics: open vulns, overdue remediations, open security incidents, exceptions |
| Vulnerabilities | `Vulnerability` linked to CI, source, severity, due date, status |
| Remediation | Task or linked Change / Ticket |
| Penetration tests | Engagement document + findings → Vulnerability or Finding |
| Security incidents | Tickets with security classification (`None` / `Suspected` / `Confirmed`) |
| Employee security concern reporting | Employee-friendly form that creates an **Incident** ticket with `Suspected` classification (same ticket engine; no second incident store) |
| Data classification | CI and Attachment classification; DLP **incidents** as tickets/events, not a DLP engine |
| DLP | Register of DLP controls and incidents; no packet inspection |
| Security awareness | Short digital modules + knowledge check + campaign assignments/completions (**not** a full LMS) |
| Risk register | `Risk` with owner, inherent/residual, treatment, linked CIs/controls |
| Security exceptions | `PolicyException` |

## Employee self-service (security)

Employees see a simple **Security** area (`/employee/security`) with:

1. **Security Awareness** — assigned modules (content + short quiz). Completion is recorded only after a successful knowledge check (default pass threshold 80%; for 3-question modules all answers must be correct). Templates are seeded as **Draft**; admins activate modules and create/open campaigns before employees are trained.
2. **Report a Security Concern** — friendly categories (phishing, account activity, lost device, malware, data disclosure, suspicious link, unauthorized access, other). Creates an Incident ticket marked `Suspected`; employees follow status in **My Requests**. Employees cannot set Critical priority.

Employee awareness and concern APIs are authenticated self-service (`/api/v1/me/security/...`) and do **not** require security admin permissions.

Ordinary employees do **not** see vulnerabilities, risk register, pentests, DLP admin, or the security dashboard.

## Security awareness model

| Entity | Role |
|--------|------|
| `AwarenessModule` | Versioned content + estimated minutes + pass threshold (seed templates: phishing, passwords, data, devices, remote) |
| `AwarenessQuestion` / `AwarenessAnswerOption` | Knowledge check (3–5 questions) |
| `AwarenessCampaign` | Draft → Open (active) → Closed; linked module/version |
| `AwarenessCompletion` | Per-user assignment (unique campaign+user); score, attempts, due/started/completed timestamps |
| `AwarenessAttempt` | Attempt history (answers stay in domain tables; BusinessAudit does not store individual answers) |
| `AwarenessReminderLog` | Deduped reminders (7 days before due, 1 day before, overdue) via Hangfire + in-app + email |

Admin UX (Security workspace): seed/activate modules, create campaigns, assign all or specific employees, close campaigns, completion drill-down, CSV export (audited).

BusinessAudit field names include: `AwarenessCampaignCreated`, `AwarenessCampaignActivated`, `AwarenessAssigned`, `AwarenessStarted`, `AwarenessAttemptSubmitted`, `AwarenessCompleted`, `AwarenessReminderSent`, plus `SecurityConcernReported` on tickets.

## ISA 315 / evidence readiness

Awareness campaign and completion records support **IT audit readiness and evidence**. Authoritative completion rows are the evidence — do not create an Evidence Library row per employee completion. Campaign/completion CSV exports may be referenced from Evidence where the existing library cleanly supports it.

Do **not** label the product or reports as “ISA 315 compliant”, “security certified”, or “audit passed”. ISA 315 language in docs means readiness support only; it does not claim statutory compliance or replace auditor judgement.

## Permissions

`sec.dashboard`, `sec.awareness.manage`, `vuln.read`, `vuln.manage`, `risk.manage`, `exception.approve`, `ticket.read.security`, `incidents.security`

Vulnerability ingest from scanners is an adapter (Phase 15/19). Security-classified ticket visibility remains gated by existing RBAC (`ticket.read.security` / `incidents.security`); employees only see their own requests.
