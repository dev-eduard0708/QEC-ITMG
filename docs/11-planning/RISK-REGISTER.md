# Program risk register (delivery)

| ID | Risk | Impact | Mitigation |
|----|------|--------|------------|
| R1 | Scope greed (all GRC in v1) | Delay | MVP cut line |
| R2 | Engine admin bypass | Security incident | Isolation + adapter-only sessions |
| R3 | Duplicate registers | Data rot | CMDB first, views later |
| R4 | SSO staging unavailable | Blocked P1 | Allow mock IdP in dev only |
| R5 | SQL licensing | Cost | Confirm edition early |
| R6 | Hangfire as attack surface | Privilege | Lock dashboard |
| R7 | File malware | Endpoint risk | Scan state machine |
| R8 | Honest compliance ignored | Misleading execs | No vanity % |
| R9 | Module boundary erosion | Unmaintainable | Architecture tests |
| R10 | Event volume | DB growth | Retention from P8 |
| R11 | Custom protocol temptation | Critical vuln | ADR-0008 |
| R12 | AI bypass RBAC | Data leak | Tool calls as user |
| R13 | Incomplete history | Audit fail | Same-transaction writes |
| R14 | MeshCentral skill gap | Integration slip | Spike in P7-00; RustDesk fallback |

Open product decisions: [../MASTER-PLAN.md](../MASTER-PLAN.md) section Open decisions.
