# System architecture

Related: [SOLUTION-STRUCTURE.md](SOLUTION-STRUCTURE.md) · [MODULAR-MONOLITH.md](MODULAR-MONOLITH.md) · [DEPLOYMENT-ARCHITECTURE.md](DEPLOYMENT-ARCHITECTURE.md) · [../MASTER-PLAN.md](../MASTER-PLAN.md)

## Context

QEC ITMG is an **internal on-premises** modular monolith:

- Browser clients (React SPA)
- ASP.NET Core application host (REST + SignalR + Hangfire)
- Microsoft SQL Server (system of record)
- File storage (local/SMB via abstraction)
- Google Workspace OIDC (primary identity); Active Directory may remain for directory sync later
- Remote-support engine (MeshCentral recommended) on a dedicated host
- SMTP for email notifications
- Reverse proxy terminating TLS

```
                     ┌─────────────────┐
  Employees/IT/Audit │  Reverse proxy  │ HTTPS
                     │  (TLS terminate)│
                     └────────┬────────┘
                              │
                     ┌────────▼────────┐
                     │  React SPA      │ static files from host or separate site
                     │  (Vite build)   │
                     └────────┬────────┘
                              │ REST + SignalR
                     ┌────────▼────────┐     ┌──────────────────┐
                     │ ASP.NET Core    │────▶│ Google OIDC      │
                     │ Modular host    │     │ (accounts.google)│
                     │ Hangfire        │     └──────────────────┘
                     └────┬───────┬────┘
                          │       │
              ┌───────────▼─┐   ┌─▼─────────────────┐
              │ SQL Server  │   │ File store         │
              │ (data+jobs) │   │ (metadata in SQL)  │
              └─────────────┘   └────────────────────┘
                          │
                     ┌────▼─────────────┐
                     │ Remote engine    │ isolated VLAN/host
                     │ (MeshCentral)    │ QEC ITMG owns authZ
                     └──────────────────┘
```

## Architectural style

| Choice | Decision |
|--------|----------|
| Application style | Modular monolith, single deployable |
| API | Versioned REST |
| Realtime | SignalR for operational live updates, not as system of record |
| Async work | Hangfire with SQL Server storage |
| Integration | Anti-corruption adapters; no direct engine UI as source of truth |
| UI | SPA; no SSR required for internal authenticated app |

Rejected for v1: microservices, Kubernetes, Kafka, event sourcing. See [ADR-0004](../12-decisions/ADR-0004-modular-monolith.md).

## Logical layers (inside the host)

1. **Presentation (API)** — HTTP endpoints, SignalR hubs, OpenAPI, authn
2. **Application** — use cases, transactions, authorization checks, mapping
3. **Domain** — entities, invariants, domain events (in-process)
4. **Infrastructure** — EF Core, file store, mail, identity, Hangfire, remote-engine client

Modules own their domain + application + infrastructure. The host **composes** modules.

## Trust boundaries

| Boundary | Rule |
|----------|------|
| Browser → API | TLS, cookie or bearer per [../04-security/AUTHENTICATION.md](../04-security/AUTHENTICATION.md), CSRF protection for cookie auth |
| API → SQL | Least-privilege DB user; parameterized access via EF; no dynamic SQL from user input |
| API → file store | Service identity; files never served from unsanitized user paths |
| API → remote engine | Service credential; engine must not trust “technician is admin in MeshCentral” as ITMG authorization |
| API → SMTP / IdP | Secrets in configuration store, not source |
| Hangfire dashboard | Restricted to Platform Administrator; not on public URL |

## Runtime processes

| Process | Responsibility |
|---------|----------------|
| `Qec.Itmg.Host` | HTTP, SignalR, module composition |
| Hangfire workers | Same host initially (separate process later if needed) |
| SQL Server | Data, Hangfire schema, optional file-index |
| Reverse proxy | TLS, headers, optional static SPA |
| MeshCentral (or equivalent) | Screen transport, agents |

Horizontal scale of the web host is possible behind the proxy with SignalR backplane (SQL or Redis) **when needed**. Do not introduce Redis in Phase 0.

## Data ownership

SQL Server holds all business data. The remote engine may hold device connectivity state; QEC ITMG holds session **authorization and audit**. Files are blobs; SQL holds attachment metadata and hashes.

## Environment topology

Dev / Staging / Production as isolated databases and configuration. Staging should include a MeshCentral (or mock) and Entra test tenant or AD test. Details: [DEPLOYMENT-ARCHITECTURE.md](DEPLOYMENT-ARCHITECTURE.md), [../10-operations/ENVIRONMENTS.md](../10-operations/ENVIRONMENTS.md).
