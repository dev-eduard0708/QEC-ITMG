# Success criteria

Related: [PRODUCT-VISION.md](PRODUCT-VISION.md) · [../11-planning/DEFINITION-OF-DONE.md](../11-planning/DEFINITION-OF-DONE.md) · [../11-planning/MVP-DEFINITION.md](../11-planning/MVP-DEFINITION.md)

Success is measured by **use and evidence quality**, not by number of screens.

## Business outcomes

1. IT staff can complete daily support without a parallel spreadsheet as the real system of record.
2. A ticket can be traced to asset/CI, remote session (if any), incident/problem/change, approvals, and attachments.
3. Privileged remote access always has identity, reason, target, time bounds, and outcome in QEC ITMG — not only in the remote engine.
4. Managers can see open work, SLA risk, and change outcomes from server-side reports.
5. Later GRC work reuses operational evidence; auditors are not given a second invented register.

## MVP success (must all be true)

| ID | Criterion |
|----|-----------|
| S1 | Users sign in via the designed SSO path; RBAC is enforced on API and UI |
| S2 | Employees can create and track service requests; technicians can work incidents and requests |
| S3 | Assets can be recorded, assigned, and linked to tickets |
| S4 | A change record can be created, approved (or standard path), implemented, and closed with history |
| S5 | Attended remote session request, consent, start/end, and audit exist even if the engine is unavailable (degraded: no connect) |
| S6 | Comments, attachments, and business audit history exist for tickets, changes, and remote sessions |
| S7 | Notifications fire for assignment, remote request, and SLA warning (in-app + email where configured) |
| S8 | A technician cannot open another user’s ticket or start remote access by guessing IDs (IDOR tests pass) |
| S9 | Break-glass and role changes are logged |
| S10 | Restore of platform backup is documented and at least once rehearsed in staging |

## Full-product success (later)

- Internal control library with multi-framework mapping and reusable evidence
- JML produces completion evidence
- DR tests, vendors, and vulnerabilities link to the same CIs
- Audit module can export an evidence pack for a period without copy-paste archaeology
- Compliance views distinguish mapped vs assessed vs evidenced states

## Anti-goals (failure if they happen)

- “Compliance score: 87%” with no methodology
- Remote engine admin console used as the real authorization path
- Duplicate application lists in CMDB, BCM, and security
- Hard-coded COBIT process IDs in application logic
- MVP delayed until every GRC module exists
