# Policy management

Related: [DOCUMENT-MANAGEMENT.md](DOCUMENT-MANAGEMENT.md) · [COMPLIANCE.md](COMPLIANCE.md)

Policies are **ManagedDocuments** with `DocumentType = Policy`. This module is the policy-specific UX and acknowledgement flows on top of document management.

## Required metadata

Number, title, type, owner, approver, version, effective date, review date, classification, status, superseded version, related controls, related framework requirements (via controls preferred), acknowledgements, attachments/body.

## Status

Draft → In Review → Approved → Published → Superseded / Retired. Historic versions immutable.

## Workflow responsibilities

Each policy carries four named responsibilities: **Policy Owner**, **Reviewer**, **Approver**, and **Publisher** (`OwnerUserId`, `ReviewerUserId`, `DesignatedApproverUserId`, `PublisherUserId`). They are set from the policy detail page, including an "assign all to me" shortcut for small teams.

Segregation of duties is **not enforced**: a single administrator may author, submit, approve, publish, and assign the same policy. This is deliberate for small IT teams. Each transition still records the acting user and timestamp on the version (`SubmittedBy*`, `ApprovedBy*`, `PublishedBy*`), so the audit trail stays complete even when one person performs every step.

Enforcing SoD (for example, approver must differ from submitter) remains an optional future policy-level setting; it is not part of the current workflow.

## Named policies (initial catalog data, not separate tables)

Information security, acceptable use, access control, password, change, backup, DR/BCP, third-party. Additional policies are records, not code.

## Acknowledgements

Optional per policy (`RequiresAcknowledgement`). Assignment is **version-specific** via `PolicyAssignment` (All Employees or Specific User). Publishing alone does **not** assign.

Employee flow: open assigned policy → read body → tick acknowledgement statement → acknowledge. Evidence stores user, version, statement key/text, timestamps, optional IP/User-Agent (supporting metadata only). Unique per `UserId + DocumentVersionId` (idempotent).

Admin: assignment counts, employee drill-down, audited CSV export.

Reminders (Hangfire): due soon (7/1 day) and overdue for outstanding required assignments.

Initial catalog (`POL-*-001`) seeds **Draft** starter templates only — require QEC Management / IT / Information Security / HR-Legal review before approve → publish → assign. Starter text is not automatically legally sufficient.

## Reviews

Hangfire: notify owner N days before `ReviewDate`. Overdue policies appear in reports.

## Permissions

`policy.read`, `policy.manage`, `policy.approve`, `policy.acknowledge`

Employees acknowledge via `/api/v1/me/policies/{id}/acknowledge` (authenticated session). Do not duplicate storage: see Document management for versioning internals.
