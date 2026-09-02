# IT operations

Related: [EVENT-MANAGEMENT.md](EVENT-MANAGEMENT.md) · [ASSET-CMDB.md](ASSET-CMDB.md) · [BUSINESS-CONTINUITY.md](BUSINESS-CONTINUITY.md)

## Purpose

Run-the-business IT: monitoring views, backups, restore tests, patching, scheduled jobs, interfaces (CI type), certificates, capacity, availability.

## Records

- OperationalEvent (see Event management)
- BackupJob definition + run results (evidence candidates)
- RestoreTest (scheduled, result, evidence)
- PatchBaseline / PatchDeployment (CI links)
- ScheduledJob (metadata; not a generic cron product)
- CertificateRecord (expiry notifications)
- CapacitySnapshot (optional later)
- Availability window / incident-derived availability

## Interfaces

System interface register = CIs of type Interface with provider/consumer CIs.

## Permissions

`ops.read`, `ops.manage`, `backup.manage`, `cert.manage`, `patch.manage`

## Non-goals

Replacing Veeam/vCenter UIs. Ingest status and link to CIs.
