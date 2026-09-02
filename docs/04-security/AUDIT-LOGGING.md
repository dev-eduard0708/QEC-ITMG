# Audit logging

Related: [../02-domain/AUDIT-HISTORY-MODEL.md](../02-domain/AUDIT-HISTORY-MODEL.md)

## SecurityAuditEvent types (minimum)

Login success/fail, logout, MFA fail, permission denied (sampled if flood), role change, permission grant, user disable, break-glass, remote start/end, unattended, evidence export, attachment download Restricted, integration secret access, settings change, Hangfire dashboard access.

## Integrity

Append-only. App user cannot delete. DB role for archive only.

## SIEM (future)

Outbound syslog/CEF. Until then, SQL + structured logs.

## Clock

UTC. Include `correlationId` from HTTP.
