# ADR-0007: Internal-control-first compliance model

Date: 2026-09-02
Status: Accepted

## Context

QEC may use COBIT, ISO/IEC 27001, NIST CSF, CIS, COSO, internal cybersecurity checklists, and auditor questionnaires. These are **not equivalent**.

## Decision

The system of record is the **Internal Control**. Frameworks and versions are data. **Control mappings** link one control to many requirements. Evidence links to controls (and optionally requirements/audits).

## Rationale

- Prevents parallel drifting checklists
- One evidence item can satisfy many mapped requirements
- New frameworks do not require schema redesign
- Avoids claiming “ISO done therefore COBIT done”

## Consequences

- Content loading is a data problem (Phase 11–12)
- Scoring must expose mapped vs assessed vs evidenced

## Alternatives considered

- One table per framework: rejected
- Hard-coded COBIT process enums: rejected
