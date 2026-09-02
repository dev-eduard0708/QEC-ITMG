# Solution structure

Related: [MODULAR-MONOLITH.md](MODULAR-MONOLITH.md) · [FRONTEND-ARCHITECTURE.md](FRONTEND-ARCHITECTURE.md) · [BACKEND-ARCHITECTURE.md](BACKEND-ARCHITECTURE.md)

This is the **intended** repository layout for Phase 0. It does not exist yet.

## Repository layout (planned)

```
/docs                         # this documentation set
/src
  /Qec.Itmg.Host              # ASP.NET Core host, composition root
  /Qec.Itmg.BuildingBlocks    # shared kernel (no module policy)
  /Qec.Itmg.Contracts         # integration events / public module contracts
  /Modules
    /Identity
    /Organization
    /Platform                 # numbering, attachments, comments, workflow, audit history
    /Notifications
    /Cmdb
    /ServiceDesk
    /ChangeManagement
    /RemoteSupport
    /AccessManagement
    /ItOperations
    /SecurityManagement
    /Governance
    /PolicyDocuments
    /Compliance
    /Evidence
    /AuditManagement
    /BusinessContinuity
    /ThirdParty
    /Reporting
    /Administration
/tests
  /Qec.Itmg.UnitTests
  /Qec.Itmg.IntegrationTests
  /Qec.Itmg.ArchitectureTests
  /Qec.Itmg.E2ETests
/frontend
  /web                        # Vite React SPA
/deploy
  /proxy
  /docker                     # optional compose for dev/staging
/scripts                      # operational scripts, not app features
```

Each backend module typically contains:

```
/Domain
/Application
/Infrastructure
/Api                          # endpoint registration, optionally
/Contracts                    # if not in global contracts
```

Do not create all module folders on day one. Phase 0 creates Host, BuildingBlocks, Identity, Organization, Platform stubs, and frontend shell. Additional modules appear when their phase starts.

## Engineering standards — C#

- File-scoped namespaces; `Qec.Itmg.{Module}.{Layer}`
- Nullable reference types enabled
- Async suffix not required; all I/O async with `CancellationToken` as last parameter
- Public APIs validate with FluentValidation at the application boundary
- Domain throws domain exceptions or returns `Result`; infrastructure exceptions are wrapped at the host
- No business logic in controllers; thin endpoints
- EF Core: no lazy loading; explicit includes; `AsNoTracking` for queries
- Transactions at use-case boundary
- Concurrency: `rowversion` on aggregates that can conflict
- All timestamps `DateTimeOffset` UTC (`UtcNow`)
- Do not swallow cancellation

## Engineering standards — React / TypeScript

- `strict` TypeScript
- Feature folders matching modules (`features/service-desk/...`)
- API client generated or hand-maintained in `src/api`; no `fetch` in random components
- TanStack Query for server state; React context only for session/theme
- Query keys factory per feature
- Forms: React Hook Form + Zod
- shadcn/ui + Tailwind; no ad-hoc CSS framework mix
- Routes gated by permission, not only hidden nav links
- i18n keys even if `en` is the only locale file initially

## Engineering standards — Git

- Branches: `main` (protected), `feat/`, `fix/`, `docs/`, `chore/`
- Commits: conventional style `feat(service-desk): ...`, `docs: ...`
- PRs: description, test plan, docs updated if behavior changes
- No secrets in git
- Do not commit `bin/`, `node_modules/`, `.env`

## Naming

| Kind | Convention |
|------|------------|
| C# types | PascalCase |
| C# methods/properties | PascalCase |
| local variables | camelCase |
| Permissions | `resource.action` lowercase dotted |
| Business numbers | `PREFIX-YYYY-NNNNNN` |
| DB tables | PascalCase singular (`Ticket`, `ConfigurationItem`) — see [../06-data/DATABASE-CONVENTIONS.md](../06-data/DATABASE-CONVENTIONS.md) |
| React components | PascalCase files |
| Hooks | `useThing.ts` |
