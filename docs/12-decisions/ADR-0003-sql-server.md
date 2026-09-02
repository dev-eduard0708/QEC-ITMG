# ADR-0003: Microsoft SQL Server

Date: 2026-09-02
Status: Accepted

## Context

Need a relational system of record with strong integrity, backups, and likely existing QEC operational skill.

## Decision

**Microsoft SQL Server** as the only OLTP database for QEC ITMG.

## Rationale

- Relational model matches tickets, CMDB graph, controls, evidence
- Rowversion, indexed views, TDE, backup/restore, Windows auth for DBA ops
- Hangfire SQL storage
- EF Core first-class provider

## Consequences

- Licensing must be planned
- Reporting stays on SQL until scale requires a replica

## Alternatives considered

- PostgreSQL: excellent, but extra operational skill vs assumed Microsoft estate
- Cosmos/Mongo as primary: poor fit for GRC relational integrity
