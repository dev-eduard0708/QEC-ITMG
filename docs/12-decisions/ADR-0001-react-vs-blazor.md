# ADR-0001: React instead of Blazor

Date: 2026-09-02
Status: Accepted

## Context

QEC ITMG needs a rich internal UI: dense tables, role-aware navigation, forms, and realtime queues. Candidates: Blazor (WASM or Server) vs React SPA.

## Decision

Use **React + TypeScript**.

## Rationale

- shadcn/ui, TanStack Table/Query, and the React ecosystem match service-desk density
- Easier hiring and contractor familiarity for this UI style
- Clear split: ASP.NET owns domain and APIs; UI remains replaceable
- Blazor Server adds sticky circuit complexity behind a proxy; WASM download size and JS interop still needed for some libraries

## Consequences

- Two languages (C# and TypeScript) — accepted
- DTO duplication mitigated by OpenAPI types
- No Blazor-specific hosting mode to operate

## Alternatives considered

- Blazor Server: faster .NET-only team, worse disconnect/proxy behavior for remote-support ops UI
- Blazor WASM: still weaker table/form ecosystem for this design system choice
