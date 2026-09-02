# Product vision

Related: [PRODUCT-SCOPE.md](PRODUCT-SCOPE.md) · [SUCCESS-CRITERIA.md](SUCCESS-CRITERIA.md) · [OUT-OF-SCOPE.md](OUT-OF-SCOPE.md) · [../MASTER-PLAN.md](../MASTER-PLAN.md)

## Statement

QEC ITMG is Quality Education Company’s internal platform for running IT as a governed operation. Every significant IT action—support, remote access, change, access grant, backup failure, policy approval—should leave a durable, attributable record that can later satisfy operations, management, and audit without reconstructing history from email and spreadsheets.

## Who it is for

| Audience | What success looks like |
|----------|-------------------------|
| Employees | Simple requests, visible ticket status, consent for remote help, knowledge articles, assigned equipment |
| IT technicians | Queues, assets, remote sessions, incidents, changes, events in one workspace |
| IT managers | SLA, workload, change success, major incidents, capacity and availability |
| Cybersecurity | Privileged activity, vulnerabilities, security incidents, exceptions, evidence |
| Compliance / GRC | Internal controls, framework mappings, assessments, reusable evidence |
| Internal / external audit | Findings, requests, evidence export, immutable history |
| Executives | Honest operational and control posture, not a vanity single percentage |

## Design thesis

IT work is a graph, not a set of isolated apps.

```
Support ticket → remote session → asset/CI → incident → problem
       → change → approval → implementation → evidence → audit trail → control
```

Modules exist for UX and ownership. They must share configuration items, people, vendors, controls, and evidence. Duplicating “the finance application” in assets, DR, vendor, and compliance registers is a product failure.

## Operating principles

1. **Operational truth first.** Service desk, CMDB, change, and access are the source of daily facts. Governance consumes those facts.
2. **Evidence is a by-product of work.** Approvals, restore tests, access reviews, and change implementations should become evidence records, not screenshots taken weeks later.
3. **One internal control library.** Frameworks map to internal controls; they do not each own a parallel checklist that drifts.
4. **Authorization stays in QEC ITMG.** Integrations (identity, remote support, monitoring) execute capabilities; they do not become a second permission system for privileged actions.
5. **On-premises first.** Architecture must run inside QEC’s network with HTTPS, without assuming cloud SaaS for core records.
6. **Honest compliance.** Mapped coverage is not assessed coverage. Assessed coverage is not certification.

## Time horizon

| Horizon | Intent |
|---------|--------|
| MVP | IT can run daily support, assets, basic incidents/changes, remote session governance, and audit trail |
| Subsequent releases | JML, operations, policy, control library, evidence, audit, security, BCM, vendors, advanced reporting |
| Later | Broader integrations, automation, AI assistance under the same RBAC |

Vision does not require delivering the full horizon in the first production release. See [../11-planning/MVP-DEFINITION.md](../11-planning/MVP-DEFINITION.md).
