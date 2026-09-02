# ADR-0011: Audit-history strategy

Date: 2026-09-02
Status: Accepted

## Context

`UpdatedAt` / `UpdatedBy` cannot answer what changed or why. Compliance records need reconstructable history.

## Decision

Implement **business audit history** as first-class records (who, what, when, old/new, source, reason) written in the **same transaction** as the change for designated aggregates.

Supplement with **security audit logs** for authn/authz/export/privileged remote.

SQL temporal tables optional later, not sufficient alone (no “why”, limited actor semantics).

## Rationale

- Auditors ask for field-level history
- Soft delete and status transitions must be visible
- Reporting and timelines can read history

## Consequences

- Volume: store diffs for significant fields, not every noisy column
- Tests must prove history is written even when notification fails after commit? Prefer same transaction; SignalR after commit.

## Alternatives considered

- Event sourcing as the write model: too heavy
- Only IIS/Serilog: not queryable as business history
