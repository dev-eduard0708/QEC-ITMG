# Test strategy

Related: [../11-planning/DEFINITION-OF-DONE.md](../11-planning/DEFINITION-OF-DONE.md)

Testing is risk-based. **Authorization and audit history are never “we'll test later.”**

## Layers

| Layer | Tooling (planned) | What |
|-------|-------------------|------|
| Unit | xUnit / NUnit, Vitest | Domain, numbering, SLA calc, mappers |
| Architecture | NetArchTest or similar | Module boundaries |
| Integration | Testcontainers or LocalDB | EF, APIs, Hangfire stubs |
| API / authz | WebApplicationFactory | IDOR, permissions |
| E2E | Playwright | Employee request, technician, consent |
| Security | ZAP/checklist, dependency scan | Phase gates |
| Performance | k6 or similar | List/search, not vanity |
| Concurrency | Parallel number allocation, If-Match | |

## Environments

CI on each PR: unit + architecture + integration (subset). Nightly: E2E + broader integration. Staging: security and performance before prod.

## Data

Deterministic seeds; no production data in CI.
