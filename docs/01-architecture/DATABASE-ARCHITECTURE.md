# Database architecture

Related: [../06-data/DATA-MODEL-OVERVIEW.md](../06-data/DATA-MODEL-OVERVIEW.md) · [../06-data/DATABASE-CONVENTIONS.md](../06-data/DATABASE-CONVENTIONS.md) · [ADR-0003](../12-decisions/ADR-0003-sql-server.md)

## Decision

**Microsoft SQL Server** is the system of record: OLTP, relational integrity, rowversion, SQL Agent optional, Hangfire storage, backup/restore familiar to QEC.

No additional operational database for MVP (no Mongo as system of record, no Elasticsearch required). Reporting runs on SQL (indexed views / query service). A replica for reporting may appear later if load requires it.

## Database per environment

Dedicated databases: `QecItmg_Dev`, `QecItmg_Staging`, `QecItmg_Prod`. No shared prod/dev data.

## Schemas

See [MODULAR-MONOLITH.md](MODULAR-MONOLITH.md). Schemas isolate modules; FKs still enforce real world relationships.

## Keys

| Kind | Type |
|------|------|
| Primary key | `uniqueidentifier` (GUID), generated in application (`UuidV7` preferred when available) or `NEWSEQUENTIALID()` |
| Business number | Separate unique constrained string |
| Concurrency | `rowversion` |
| FK | Typed uniqueidentifier |

## History

Do not use temporal tables as the **only** business audit mechanism (they are a useful supplement). Business audit history is an explicit table pattern: [../02-domain/AUDIT-HISTORY-MODEL.md](../02-domain/AUDIT-HISTORY-MODEL.md).

SQL Server temporal tables **may** be enabled on selected high-risk tables later; they are not a substitute for who/why.

## Polymorphic references

Allowed only for:

- Attachments (`OwnerType` + `OwnerId`) with a controlled owner-type enum
- Comments / timeline
- Workflow instance attachment to an aggregate
- Evidence links to multiple aggregate kinds
- Notifications (`SourceType` + `SourceId`)

Not allowed for: ticket→CI (use `ConfigurationItemId`), change→CI (link table), vendor on asset (FK).

## High-volume data

`ops.OperationalEvent` may grow quickly. Design:

- Partitioning or monthly archive tables from Phase 8
- Retention job
- Do not store raw syslog in ITMG

## Security

- TDE where SQL edition and policy require
- Separate app login vs migration/admin login
- App login: no `db_owner` in production
- Secrets never in tables in plaintext (use wrapping / DPAPI / vault)

## Backups

Platform backup is a first-class operational concern: [../10-operations/BACKUP-RESTORE.md](../10-operations/BACKUP-RESTORE.md).
