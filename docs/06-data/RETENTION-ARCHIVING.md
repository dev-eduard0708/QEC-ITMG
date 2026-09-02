# Retention and archiving

## UI rules

| Class | Soft delete | Hard delete in UI | Archive |
|-------|-------------|-------------------|---------|
| Tickets, changes, problems | Yes (admin) | No | Optional after N years |
| Audit history, security log | No | No | DBA export |
| Evidence, findings, assessments | No (withdraw/supersede) | No | Legal hold flag |
| OperationalEvent | Not typically | Purge job | Yes, aggressive |
| Notifications | User dismiss | Purge old | Yes |
| Draft unused lookups | Yes | Yes if unused | n/a |

## Suggested periods (configure; legal/QEC policy overrides)

- Tickets/changes: 7 years
- Evidence: max(control frequency * 3, audit cycle + 1 year)
- Security logs: 1–3 years online
- Events: 90–180 days hot

Jobs implement retention; users cannot empty the audit trail.

## Archive

Move cold events to `ops.OperationalEventArchive`. Keep ids for links.
