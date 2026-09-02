# Backup and restore (platform)

## What

1. SQL Server full + log backups
2. File store volume
3. Reverse proxy certs (elsewhere)
4. Engine (MeshCentral) **separate** backup — restore order documented

## Consistency

App briefly drain or accept “files slightly ahead of SQL” with orphan GC. Prefer backup SQL then files, or volume snapshot if available.

## Restore

Documented runbook; **rehearse in staging** as MVP success criterion S10.

Encryption and access control on backup media.
