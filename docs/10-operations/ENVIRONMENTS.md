# Environments

| Env | Purpose | Identity | Engine | Data |
|-----|---------|----------|--------|------|
| Development | Dev machines | Dev tenant / mock | Mock or local MeshCentral | Fake |
| Staging | Pre-prod, E2E, restore tests | Test Entra/AD | Lab MeshCentral | Anonymized-like seed |
| Production | QEC | Real Entra/AD | Prod engine | Real |

No shared databases. Config via environment. See [CONFIGURATION.md](CONFIGURATION.md).
