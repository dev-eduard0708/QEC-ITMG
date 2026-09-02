# ADR-0009: File storage strategy

Date: 2026-09-02
Status: Accepted

## Context

Tickets, evidence, policies, and audit exports need files. Storing blobs in random tables or unsanitized folders is unsafe.

## Decision

**SQL metadata + blob storage abstraction** (`IFileStorage`). Default: local/SMB disk. Hashes, classification, malware scan states, authorization via parent record.

## Rationale

- SQL backups stay smaller
- Storage can move without schema redesign
- Central malware and download audit

## Consequences

- Backup must include files + database (consistent procedure)
- Scan engine wiring may lag; state machine still exists from day one

## Alternatives considered

- All files in `varbinary`: simple but poor for large evidence
- Per-module folders with original names: rejected (IDOR and overwrite risk)
