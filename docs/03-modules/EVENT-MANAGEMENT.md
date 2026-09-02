# Event management

Related: [IT-OPERATIONS.md](IT-OPERATIONS.md) · [INCIDENT-MANAGEMENT.md](INCIDENT-MANAGEMENT.md)

## Definition

An **event** is an observable occurrence, typically from monitoring, backup, certificate expiry, or jobs. Events are **not** tickets until promoted.

## Aggregate

`OperationalEvent`: `EVT-YYYY-NNNNNN`, source, severity, CI, title, payload summary (not raw dump), first/last seen, count (dedup), status, linked ticket id.

## Deduplication

Integrations should send a `SourceEventKey`. Same key updates last-seen and increment rather than flooding.

## Correlation

Simple rules in IT Operations phase: severity + CI + window. Not a full AIOps product in v1.

## Retention

High volume: archive/purge per [../06-data/RETENTION-ARCHIVING.md](../06-data/RETENTION-ARCHIVING.md). Do not use Events as a SIEM.

## Permissions

`event.read`, `event.acknowledge`, `event.promote`, `event.admin` (rule config)
