# ADR-0005: REST + SignalR

Date: 2026-09-02
Status: Accepted

## Context

Need a documented API for the SPA and future integrations, plus live operational updates.

## Decision

**HTTP REST** as the system API. **SignalR** for push notifications of small events. OpenAPI for contract.

## Rationale

- REST is operable with reverse proxies, audit, and simple clients
- SignalR is native to ASP.NET and sufficient for queues
- GraphQL is optional later; not required for MVP
- gRPC-web is poorer for browser CRUD and OpenAPI culture

## Consequences

- Versioning via `/api/v1`
- SignalR is best-effort; REST remains source of truth

## Alternatives considered

- GraphQL-first: extra authz complexity on field level for GRC
- Only SignalR: not suitable for commands and files
