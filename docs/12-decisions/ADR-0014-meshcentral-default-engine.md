# ADR-0014: MeshCentral as default remote-support engine

Date: 2026-09-02
Status: Accepted (engine choice; adapter remains mandatory)

## Context

Candidates: RustDesk, MeshCentral, Apache Guacamole. QEC needs attended consent, unattended for managed systems, on-prem, and ITMG-owned authorization.

## Decision

**Recommend MeshCentral** as the default engine for architecture and first integration.

## Comparison

| Criterion | MeshCentral | RustDesk | Guacamole |
|-----------|-------------|----------|-----------|
| Self-hosted | Yes | Yes (ID/relay/hbbs) | Yes |
| Browser technician console | Strong | Weaker (native client focus) | Strong (HTML5) |
| Agent on Windows endpoints | Yes | Yes | No (uses RDP/VNC/SSH) |
| Attended consent UX | Achievable via agent + ITMG gate | Achievable | Depends on OS RDP session |
| Unattended servers | Strong | Strong | Strong if RDP/SSH allowed |
| API / automation | Good | Improving; more DIY | Extensions / session recording add-ons |
| Extra client install for techs | Optional | Typical | No |
| Jump-host to agentless network devices | Weaker | Weaker | Strong (SSH/RDP) |

## Rationale

MeshCentral matches an **IT management** posture: agents, groups, web console, file transfer, and APIs. QEC ITMG still **must not** treat MeshCentral rights as platform rights.

RustDesk remains the fallback if QEC standardizes on its native client performance.

Guacamole is the planned **complement** for SSH/RDP to systems that will not run an agent (later adapter).

## Consequences

- Dedicated engine host, isolated admin UI
- Device id mapping: `cmdb` CI ↔ engine node id
- Session start only after ITMG issues a short-lived authorization token/record
- ITMG integrates via MeshCtrl-compatible **control.ashx** WebSocket and native **/meshagents** URLs — not invented REST such as `api/mesh/sessions`
- Desktop join uses MeshCentral UI: `/?viewmode=11&gotonode={nodeId}`
- See [MESHCENTRAL-REMOTE-SUPPORT.md](../10-deployment/MESHCENTRAL-REMOTE-SUPPORT.md)

## Alternatives considered

- Leading with Guacamole: poorer employee attended-support story
- Leading with RustDesk: acceptable alternative; document if QEC already standardized on it
