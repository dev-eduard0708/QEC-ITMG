# Deployment architecture

Related: [SYSTEM-ARCHITECTURE.md](SYSTEM-ARCHITECTURE.md) · [../10-operations/ENVIRONMENTS.md](../10-operations/ENVIRONMENTS.md) · [../10-operations/RELEASE-STRATEGY.md](../10-operations/RELEASE-STRATEGY.md)

## Principles

- Internal network only for the management plane
- HTTPS everywhere (user → proxy → optional HTTP to Kestrel on localhost)
- Three environments: Development, Staging, Production
- Docker is **optional packaging**, not an orchestrator mandate
- Ability to relocate the remote engine, file store, or later a reporting replica without redesigning domain APIs

## Logical nodes (production)

| Node | Function |
|------|----------|
| Reverse proxy | TLS, HSTS, headers, optional WAF rules, static SPA |
| App server | ASP.NET Core host + Hangfire |
| SQL Server | Database + backups |
| File share / volume | Attachment blobs |
| Identity | Entra ID / AD |
| Remote engine host | MeshCentral (recommended), isolated |
| SMTP | Mail relay |

App server and SQL may be the same VM in small deployments; prefer split for production.

## Reverse proxy

Terminate TLS with QEC certificates. Forward:

- `/` SPA
- `/api` and `/hubs` to Kestrel
- WebSocket support for SignalR
- Do not expose Hangfire dashboard on `/hangfire` without additional IP/auth restriction

Security headers: `Content-Security-Policy`, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`. See [../04-security/SECURITY-ARCHITECTURE.md](../04-security/SECURITY-ARCHITECTURE.md).

## Docker

Makes sense for:

- Repeatable **staging** and developer dependencies (SQL, Mailhog, MeshCentral)
- Optional app container behind proxy

Does not require Kubernetes. Compose is enough for non-prod. Production may run the app as Windows Service or container on a single Docker host — operations chooses; architecture remains the same.

## Configuration

Environment variables or ASP.NET configuration files **outside** the image. No secrets in images. See [../10-operations/CONFIGURATION.md](../10-operations/CONFIGURATION.md).

## Moving components later

| Component | How it can move |
|-----------|-----------------|
| SPA | Separate static host; same API origin or CORS locked to that origin |
| Hangfire workers | Second process, same DB |
| File store | Swap `IFileStorage` to SMB/S3-compatible |
| Remote engine | Swap adapter; session records unchanged |
| Identity | OIDC configuration |
| Reporting | Read replica / nightly warehouse — Reporting module already read-only |

## Network segmentation

Remote engine agents are highly sensitive. Recommend:

- Engine admin UI not reachable by all employees
- Only the QEC ITMG app server talks to engine API
- Technicians use ITMG UI; engine UI is break-glass for platform admins
