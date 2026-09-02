# Security management

Related: [INCIDENT-MANAGEMENT.md](INCIDENT-MANAGEMENT.md) · [../04-security/SECURITY-ARCHITECTURE.md](../04-security/SECURITY-ARCHITECTURE.md) · [../05-compliance/CYBERSECURITY-CHECKLIST-STRATEGY.md](../05-compliance/CYBERSECURITY-CHECKLIST-STRATEGY.md)

## Purpose

Operational cybersecurity records **on top of CMDB and tickets**, not a parallel asset list.

## Capabilities

| Area | Model |
|------|--------|
| Security dashboard | Server metrics: open vulns, overdue remediations, open security incidents, exceptions |
| Vulnerabilities | `Vulnerability` linked to CI, source, severity, due date, status |
| Remediation | Task or linked Change / Ticket |
| Penetration tests | Engagement document + findings → Vulnerability or Finding |
| Security incidents | Tickets with security classification |
| Data classification | CI and Attachment classification; DLP **incidents** as tickets/events, not a DLP engine |
| DLP | Register of DLP controls and incidents; no packet inspection |
| Awareness | Training campaigns + completion records (not full LMS) |
| Risk register | `Risk` with owner, inherent/residual, treatment, linked CIs/controls |
| Security exceptions | `PolicyException` |

## Permissions

`sec.dashboard`, `vuln.read`, `vuln.manage`, `risk.manage`, `exception.approve`, `ticket.read.security`

Vulnerability ingest from scanners is an adapter (Phase 15/19).
