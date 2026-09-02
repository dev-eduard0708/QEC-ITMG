# ADR-0012: Hangfire for background work

Date: 2026-09-02
Status: Accepted

## Context

SLA, notifications, expiry sweeps, and later integrations need reliable background work on-premises.

## Decision

Use **Hangfire** with **SQL Server** storage, hosted in the app (split worker process later if needed). Dashboard restricted to Platform Administrator.

## Rationale

- Delayed and recurring jobs, retries, visibility
- Fits SQL-centric operations
- Avoids inventing a queue

## Consequences

- Hangfire schema in the same or dedicated database (same SQL instance recommended)
- Jobs must be idempotent and authorization-aware

## Alternatives considered

- Quartz.NET, raw hosted services, cloud-only schedulers
