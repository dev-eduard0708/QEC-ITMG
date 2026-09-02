# Status models

Statuses are **data** driven by workflow definitions where possible. The following are the canonical initial sets so modules do not invent synonyms (`Closed` vs `Close` vs `Done`).

## Ticket (incident and service request)

`New → Triaged → InProgress → OnHold → Resolved → Closed`

- `Cancelled` from New/Triaged/OnHold
- `Reopened` is a transition **into** InProgress from Resolved/Closed (with reason), not a stored status
- Security incidents use the same statuses; extra fields for severity/Triage

SLA pauses on `OnHold` only for approved hold reasons (waiting on user, waiting on vendor).

## Problem

`New → Investigating → RootCauseIdentified → KnownError → Resolved → Closed`

`Cancelled` when invalid.

## ChangeRequest

`Draft → Assessment → Approval → Scheduled → Implementation → Validation → PostImplementationReview → Closed`

Failure/side states (terminal or holding):

- `Rejected` (from Approval)
- `Failed`
- `RolledBack`
- `RequiresFollowUp`
- `Cancelled` (from Draft/Assessment)

Emergency changes still pass through these states with shortened approval; `PostImplementationReview` is **mandatory** before Closed.

Standard changes may skip Assessment and use pre-authorized Approval (recorded as auto-approved with catalog reference).

## RemoteSessionRequest

`Requested → NotifyUser → Allowed | Declined | Expired → (if Allowed) Connecting → InSession → Ended`

Unattended: `Requested → Authorized (MFA) → Connecting → InSession → Ended` (no user Allow). `Denied` if policy fails.

Outcome on session: `Completed`, `Failed`, `TerminatedByUser`, `TerminatedByTechnician`, `TerminatedBySystem`.

## AccessCase (JML / access request)

`Draft → Submitted → Approval → Fulfillment → Verification → Closed`

`Rejected`, `Cancelled`. Leaver cases cannot be Cancelled after fulfillment starts without IT Manager override (audited).

## ManagedDocument / Policy

`Draft → InReview → Approved → Published → Superseded | Retired`

## Evidence

`Draft → Submitted → Accepted → Expired | Superseded | Withdrawn`

`Accepted` is the reusable state. Expired is computed or set by job when `ValidTo < UtcNow`.

## ControlAssessment

`NotStarted → InProgress → Complete`

Result: `Compliant | PartiallyCompliant | NonCompliant | NotApplicable | NotTested`

Do not equate `NotTested` with `Compliant`.

## Finding

`Open → InRemediation → PendingVerification → Closed | AcceptedRisk`

`AcceptedRisk` requires Exception link.

## CorrectiveAction

`Open → InProgress → Completed → Verified`

`Overdue` is a computed flag, not a status.

## OperationalEvent

`Open → Acknowledged → Linked (to ticket) → Closed` (noise events may auto-close)

## Risk

`Identified → Analyzed → Treatment → Monitoring → Closed`

## VendorAssessment

`Scheduled → InProgress → Review → Complete`

## Soft-deleted records

Status is not `Deleted`. Soft delete is a flag; lists exclude them. Status remains last business status.
