# Evidence library

Related: [../05-compliance/EVIDENCE-STRATEGY.md](../05-compliance/EVIDENCE-STRATEGY.md) · [../01-architecture/FILE-STORAGE-ARCHITECTURE.md](../01-architecture/FILE-STORAGE-ARCHITECTURE.md)

## Purpose

Reusable evidence with validity, sensitivity, links to controls, audits, and operational sources.

## Example

“Q2 Privileged Access Review” (`EVD-2026-000042`) may satisfy ISO, COBIT mappings, internal audit, and a cybersecurity checklist question **via links**, not copies.

## Metadata

Owner, source (`Manual`, `Ticket`, `Change`, `AccessReview`, `DrTest`, `BackupRestore`, `Export`), type, period (`ValidFrom`/`ValidTo`), captured at, classification, version, linked controls, linked audits, history.

## Files

Attachments through central service. Screenshots, reports, approvals, configs, logs, test results are **types**, not separate storage silos.

## Operational capture

When a restore test completes or an access review closes, a use case **offers** “promote to evidence” (or auto-create in later automation). MVP of those modules should at least allow manual linking.

## Permissions

`evidence.read`, `evidence.upload`, `evidence.accept`, `evidence.export` (highly privileged, audited)

Export is an audit event. No bulk export for users with only `evidence.read`.
