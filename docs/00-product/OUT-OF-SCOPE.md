# Out of scope

Related: [PRODUCT-SCOPE.md](PRODUCT-SCOPE.md) · [../11-planning/MVP-DEFINITION.md](../11-planning/MVP-DEFINITION.md)

This list prevents accidental expansion during implementation.

## Never in this product (as currently envisioned)

| Item | Reason |
|------|--------|
| Custom remote desktop protocol | Insecure and unnecessary; see [ADR-0008](../12-decisions/ADR-0008-remote-support-integration.md) |
| Public multi-tenant SaaS | Internal QEC platform |
| Replacing Entra ID / AD as the identity provider | QEC ITMG is an application, not the IdP |
| Full HRIS / payroll | JML consumes HR signals; it does not become HR |
| Full procurement / ERP | Vendors and assets may integrate later; ERP is not rebuilt |
| Legal e-discovery platform | Evidence library is GRC/IT evidence, not litigation hold |
| SIEM replacement | May **send** events; does not store all telemetry |
| Network packet capture / NDR | Out of platform scope |
| Endpoint protection replacement | Inventory and findings may ingest; agents stay with existing EPP |
| Certifying QEC against ISO/COBIT | Tooling supports the program; certification is organizational |
| Training LMS replacement | Security awareness tracking may record completions, not host all courses |
| Building a universal BPM/workflow product | Scoped workflow engine only; [DOMAIN-MODEL](../02-domain/DOMAIN-MODEL.md) |

## Out of MVP (allowed later)

- Microservices, Kubernetes, Kafka, event sourcing, full CQRS stacks
- AI features
- Teams / SMS / mobile push as required channels
- Unattended remote as a broad technician capability
- Full framework content packs and assessment campaigns
- External auditor portal
- Customer-facing knowledge portal on the public internet
- Automatic AD provisioning for every joiner (may start as checklist + evidence)

## Out of current documentation task

- Application source code
- EF Core migrations
- Running APIs
- Scaffolded React pages
- Package installation for an app that does not exist yet

## Architecture anti-patterns explicitly rejected

Documented in [../01-architecture/ARCHITECTURE-DECISIONS.md](../01-architecture/ARCHITECTURE-DECISIONS.md) and ADRs:

- Microservices for the first deployment
- Generic `EntityType` + `EntityId` for core domain relationships that should be foreign keys
- Per-framework duplicate control tables
- Storing binaries in arbitrary module tables without the file service
- Relying only on `UpdatedAt` / `UpdatedBy` for auditability
