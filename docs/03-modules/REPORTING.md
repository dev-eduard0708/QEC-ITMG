# Reporting

Related: [../08-ux/INFORMATION-ARCHITECTURE.md](../08-ux/INFORMATION-ARCHITECTURE.md)

## Principle

Reports are **server-side queries** (and later snapshots). The SPA does not download all tickets to compute MTTR.

## Report groups

| Group | Examples |
|-------|----------|
| Service desk | Open by priority, FRT, resolution, SLA breach, reopen, backlog, workload |
| Incidents | Major, MTTA, MTTR, recurring, trends |
| Changes | Success, fail, emergency, unauthorized, rollback |
| Assets | Inventory, lifecycle, warranty, unassigned, compliance flags |
| Security | Vulns, aging, incidents, overdue actions |
| Governance | Control coverage (honest), policy status, reviews, risk |
| Audit | Findings, overdue CAPA, evidence gaps |
| BCM | RTO/RPO coverage, DR tests, failed tests |
| Vendors | Assessments, expiry, risk |

## Implementation

Reporting module: query services, indexes, optional nightly snapshot tables for executive dashboards (Phase 18). MVP: a few operational SQL queries behind `/api/v1/reports/...`.

## Permissions

Each report requires a permission (`report.servicedesk`, `report.compliance`, …). No “all reports” for Employee role.

## Anti-pattern

A single unexplained “compliance %” tile. See Compliance module.
