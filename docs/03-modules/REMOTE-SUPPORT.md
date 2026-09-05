# Remote support

Related: [ADR-0008](../12-decisions/ADR-0008-remote-support-integration.md) · [ADR-0014](../12-decisions/ADR-0014-meshcentral-default-engine.md) · [../04-security/REMOTE-ACCESS-SECURITY.md](../04-security/REMOTE-ACCESS-SECURITY.md) · [MeshCentral deployment](../10-deployment/MESHCENTRAL-REMOTE-SUPPORT.md)

## Ownership split

| QEC ITMG owns | MeshCentral owns |
|---------------|------------------|
| Who may request | Screen/input transport |
| Attended vs unattended policy | Agents, codecs, NAT traversal |
| Ticket / change / reason | Device online state |
| User consent record | Node registration |
| Technician identity (ITMG user) | Desktop viewer |
| Start/end/outcome audit | |
| Session chat | |
| On-demand `RemoteEndpoint` + enrollment | |

## Integration type (honest)

ITMG talks to MeshCentral using **documented MeshCtrl-compatible mechanisms**:

- WebSocket `control.ashx` with `x-meshauth` (list/probe nodes)
- Native agent URL `/meshagents?id={type}&meshid={group}`
- Desktop join URL `/?viewmode=11&gotonode={nodeId}`

There is **no** invented ITMG-only REST like `api/mesh/sessions`.

| Area | Status |
|------|--------|
| Identity-first Get Remote Help / chat / consent | Implemented in ITMG |
| One-time enrollment + Support Helper package | Implemented in ITMG |
| Real MeshCentral control client + agent URL | Implemented in ITMG |
| Live MeshCentral server verification | Requires external deployment |

## Identity-first eligibility

Authenticated Active Employee + `remote.self.request`. Production domain gate remains `Authentication:Oidc:AllowedDomains`. Remote Support does not hardcode email suffixes.

## Device modes

**Managed:** CI `RemoteEngineNodeId` mapping — skip helper when Ready.

**On-demand:** Temporary `RemoteEndpoint` via Support Helper; attended only; no auto CI; unattended blocked.

## Attended connect

Device Ready → technician selects endpoint → Request Remote Access → employee Allow → Connect → MeshCentral join URL.

## Permissions

`remote.self.request`, `remote.request`, `remote.attended`, `remote.unattended`, `remote.audit.read`, `remote.admin`
