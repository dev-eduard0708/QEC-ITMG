# Backend architecture

Related: [MODULAR-MONOLITH.md](MODULAR-MONOLITH.md) · [../07-api/API-DESIGN-STANDARDS.md](../07-api/API-DESIGN-STANDARDS.md) · [ADR-0005](../12-decisions/ADR-0005-rest-and-signalr.md)

## Host

`Qec.Itmg.Host` responsibilities:

- Kestrel / reverse-proxy integration
- Authentication middleware (OIDC)
- Authorization policies mapped to permissions
- Exception handler → [error contract](../07-api/ERROR-CONTRACT.md)
- OpenAPI (filtered by environment)
- Health checks (`/health/live`, `/health/ready`)
- Serilog (or equivalent) structured logging
- Hangfire server + dashboard (restricted)
- SignalR hubs
- Module registration (`IModule.Register(IServiceCollection)`)

## Request pipeline (command)

1. Authenticate
2. Authorize endpoint permission
3. Bind and FluentValidate
4. Load aggregate with concurrency token
5. **Resource-level** authorization
6. Domain mutation
7. Persist + business audit history (same transaction)
8. Outbox / in-process events (notifications, SignalR)
9. Return DTO

Queries skip mutations but still authorize and must not leak fields (internal notes, security incident data).

## Validation

- FluentValidation for commands
- Data annotations not used as the primary rule engine
- Zod on frontend is UX; server validation is authoritative

## Errors

- Domain: `NotFound`, `Conflict`, `Forbidden`, `Validation`, `Invariant`
- Map to HTTP in one place
- No stack traces to clients

## Background processing (Hangfire)

Chosen over raw `IHostedService` queues and over Azure-only schedulers because QEC is on-prem and already has SQL Server. See [ADR-0012](../12-decisions/ADR-0012-hangfire.md).

Uses:

- SLA clock ticks and breach detection
- Notification send/retry
- Evidence expiry sweep
- Certificate/contract expiry warnings
- Integration pulls (later)
- Report snapshot jobs (later)

Jobs must:

- Be idempotent
- Carry `correlationId`
- Run as a **service identity** with explicit permissions
- Not bypass RBAC when acting “on behalf of” a user (store acting user id)

## Persistence

- EF Core, SQL Server
- One `DbContext` per module **or** one context with schemas — prefer **per-module DbContext** + migrations per module to preserve boundaries
- Shared kernel entities configured once
- Migrations checked in; never auto-migrate production on startup (apply in release pipeline)

## Caching

- Not required for MVP beyond TanStack Query
- If added: cache authorization permission set per user with short TTL after role change

## OpenAPI

- Generate from endpoints
- Version in path `/api/v1`
- Security schemes documented
- Do not expose Hangfire or admin debug endpoints in the public spec
