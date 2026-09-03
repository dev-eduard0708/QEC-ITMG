# Architecture decisions (summary)

Canonical records live in [../12-decisions/ADR-INDEX.md](../12-decisions/ADR-INDEX.md). This file is the narrative summary.

## Chosen stack

| Topic | Choice | ADR |
|-------|--------|-----|
| UI framework | React + TypeScript, not Blazor | 0001 |
| SPA tooling | Vite, not Next.js | 0002 |
| Database | SQL Server | 0003 |
| Decomposition | Modular monolith | 0004 |
| API + live updates | REST + SignalR | 0005 |
| CMDB | Central CI graph | 0006 |
| Compliance | Internal-control-first | 0007 |
| Remote access | Integrate MeshCentral (adapter), not a custom protocol | 0008 |
| Files | Metadata in SQL, blob via `IFileStorage` | 0009 |
| Authentication | Google OIDC BFF + app SQL RBAC | 0010 |
| History | Explicit business audit history | 0011 |
| Jobs | Hangfire + SQL | 0012 |
| Charts | Recharts | 0013 |

## Explicitly not chosen (v1)

- Microservices, Kubernetes, Kafka
- Event sourcing / full CQRS infrastructure
- Universal BPM suite
- Generic entity engine
- Blazor Server as primary UI
- Storing files only in SQL `varbinary` for all content (allowed only for tiny generated artifacts if ever — default is blob store)
- Engine-native users as the authorization model for remote access

## Hangfire vs alternatives

| Option | Verdict |
|--------|---------|
| Hangfire | **Selected** — mature, SQL storage, dashboard, retries, delayed jobs |
| Quartz.NET | Viable; weaker dashboard/ops story for this team |
| Channel + hosted service | Too little for SLA, retries, visibility |
| Azure Functions | Not on-prem first |

## Remote engine evaluation (summary)

| Product | Fit | Notes |
|---------|-----|-------|
| MeshCentral | **Recommended default** | Web UI, agents, APIs, attended + unattended, self-hosted, maps to device inventory |
| RustDesk | Strong alternative | Excellent desktop performance; extra client; self-host ID/relay; API/governance still must be wrapped |
| Apache Guacamole | Complementary later | HTML5 gateway for RDP/VNC/SSH; weaker “user consent on agent” product story; good jump-host |

QEC ITMG always owns authorization, ticket linkage, consent records, and audit. Details: [../03-modules/REMOTE-SUPPORT.md](../03-modules/REMOTE-SUPPORT.md).

## Workflow balance

A **small workflow engine**: definition (states, transitions, required permissions, optional approver role), instance per record, comments on transition. Not Camunda. MVP workflows: ticket status, change lifecycle, remote consent, later policy/JML.

## Clock

`IClock` for testability. Persist UTC. Display via organization default timezone and user preference.
