# ADR-0006: Centralized CMDB

Date: 2026-09-02
Status: Accepted

## Context

Applications, servers, and services appear in support, DR, security, vendors, and compliance. Duplicate registers diverge immediately.

## Decision

A **single CMDB** (Configuration Items + typed relationships + business services). Assets overlay financial/custody data, possibly 1:1 with a CI.

## Rationale

- One source of truth principle
- Incidents, changes, RTO/RPO, vulnerabilities, and vendors all hang off CIs
- Avoids “Finance App” existing four times

## Consequences

- Cmdb module is a Phase 3 dependency for almost everything after
- Other modules store FKs, not copies of CI master data

## Alternatives considered

- Per-module registers: rejected
- Only assets without CI relationships: insufficient for dependency mapping
