# Audit history model

Related: [ADR-0011](../12-decisions/ADR-0011-audit-history.md) · [../04-security/AUDIT-LOGGING.md](../04-security/AUDIT-LOGGING.md)

## Two channels

| Channel | Table (logical) | Purpose |
|---------|-----------------|--------|
| Business audit history | `plt.BusinessAuditRecord` | Reconstruct what changed on a business record |
| Security audit log | `plt.SecurityAuditEvent` | Authn, authz failures, privilege, export, admin |

Do not mix them into one untyped dump.

## BusinessAuditRecord fields

- Id
- AggregateType (enum)
- AggregateId
- BusinessNumber (denormalized for search)
- OccurredAtUtc
- ActorUserId (null for system jobs → `ActorType = System`, `JobName`)
- Source (`Ui`, `Api`, `Job`, `Integration`)
- Action (`Created`, `Updated`, `StatusChanged`, `Assigned`, `SoftDeleted`, `Restored`, `Linked`, `ConsentGranted`, …)
- FieldName (null for non-field actions)
- OldValue (string/JSON, redacted if secret)
- NewValue
- Reason (required for privileged/status reverse)
- CorrelationId
- ClientIp (optional)

## What must be history-covered from Phase 1 onward

Identity role assignments, permission changes, user disablement.

Then every MVP aggregate: Ticket, Asset/CI, ChangeRequest, RemoteSessionRequest/Session, Attachment metadata (not blob), Notification preferences admin overrides.

Later: Evidence, Control, Assessment, Audit, JML, etc. as those modules appear.

## What not to store

- Passwords, tokens, MFA secrets
- Full file contents
- Unchanged fields
- High-churn fields unless security relevant (e.g. skip storing every SignalR ping)

## Immutability

No UPDATE/DELETE via application for these tables. No UI “edit history.” Retention/archiving is a DBA/job procedure, not user delete.

## Timeline vs history

Employee timeline shows a subset (public comments, status, assignments). Internal notes and field diffs with Confidential data require `ticket.read.internal` or equivalent.

## Transaction rule

Write history in the **same SQL transaction** as the business update. If history insert fails, the mutation fails.

## Testing

Authorization tests + history tests are mandatory for status transitions and role changes. See [../09-testing/SECURITY-TESTING.md](../09-testing/SECURITY-TESTING.md).
