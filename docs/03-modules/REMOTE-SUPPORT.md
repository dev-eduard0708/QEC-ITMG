# Remote support

Related: [ADR-0008](../12-decisions/ADR-0008-remote-support-integration.md) · [ADR-0014](../12-decisions/ADR-0014-meshcentral-default-engine.md) · [../04-security/REMOTE-ACCESS-SECURITY.md](../04-security/REMOTE-ACCESS-SECURITY.md)

## Ownership split

| QEC ITMG owns | Engine owns |
|---------------|-------------|
| Who may request | Screen/input transport |
| Attended vs unattended policy | Agents, codecs, NAT traversal |
| Ticket / change / reason | Optional session recording if enabled |
| User consent record | Device online state |
| Technician identity (ITMG user) | Engine node connection |
| Start/end/outcome audit | File transfer **if exposed** — copy into ITMG audit when API allows |
| MFA step-up for unattended | |
| Session chat transcript + audit | Engine-side chat **if any** — not used |

The engine **must not** be a hidden bypass. Operational control: technicians are not Domain Admins on MeshCentral; only the app’s service account creates sessions. Engine UI is break-glass.

## Mapping

`ConfigurationItem.RemoteEngineNodeId` (or mapping table). No session without a CI (create CI first).

## Employee agent onboarding

`GET /api/v1/me/remote-support/onboarding` returns readiness per assigned device plus the agent download/instructions from `RemoteSupportOptions`. The employee UI (`/employee/remote-support` → `/employee/remote-support/setup`) is deliberately jargon-free: no engine, node, or MeshCentral wording.

Readiness per device:

| `readinessStatus` | Meaning | Who acts |
|-------------------|---------|----------|
| `Ready` | CI mapped and engine healthy | Nobody — IT may request a session |
| `SetupRequired` | Device linked to a CI but not mapped | Employee installs the agent (one-time per device) |
| `WaitingForIt` | Mapping exists but engine is disabled/unconfigured | IT |
| `DeviceNotLinked` | Asset has no CI | IT registers the CI |

`overallStatus` drives the employee page CTA: anything other than `Ready` shows **Set up Remote Support**. `AgentNotConfigured` means no download URL is configured, so the setup page states that Remote Support is not configured yet instead of implying the employee can fix it. Readiness is never reported as `Ready` on the strength of an install the platform cannot observe — only a real CI mapping plus a healthy engine produces `Ready`.

Ownership split for onboarding: ITMG owns asset→CI linkage, CI→node mapping (`remote.admin`, via CMDB), and the agent download/instruction configuration. The engine owns agent installation mechanics and reporting the node online.

## Session chat

Chat is a first-class ITMG feature, not an engine passthrough, so the transcript stays inside ITMG audit even when the engine is unavailable.

- `GET /api/v1/remote-support/sessions/{id}/messages` — full history (source of truth, reloaded on every mount)
- `POST /api/v1/remote-support/sessions/{id}/messages` — persist a user message
- SignalR hub `/hubs/remote-support` (`JoinSession`, `LeaveSession`, event `remoteChatMessage`) — live delivery only

The web client persists through REST, subscribes to the hub for live updates, and falls back to polling every 8s when the hub cannot be reached. System messages (`messageType: System`) mark lifecycle events — requested, allowed, declined, expired, connecting, started, ended, failed — so a session that never connected still reads as an explainable conversation.

Chat availability spans request creation through a 7-day post-end window. Chat is **not** consent: the employee consent banner keeps explicit Allow / Decline actions, and the UI states that chatting does not approve the connection.

## Attended flow

1. Ticket exists (typical).
2. Technician `remote.request` → `RemoteSessionRequest` (reason, ticket, CI, requested privileges).
3. User notified (in-app + email).
4. User Allow / Decline (authenticated as requester or device user).
5. On Allow, adapter starts engine session; store engine session id.
6. On end (webhook or poll), record duration, outcome, elevation, files if known.
7. Consent evidence stored (who, when, IP).

Decline and expiry are terminal for that request; technician may create a new request.

## Unattended flow

Allowed only if:

- CI tagged `UnattendedRemotePermitted` **and** policy class (server, kiosk, IT-managed)
- Role has `remote.unattended`
- MFA step-up succeeded this session
- Business reason required
- Ticket **or** Change linked (configuration: required for production criticality)

Employee users never see unattended.

## Degraded mode

If engine is down: request/consent still persist; connect button fails clearly. Audit still valid. Chat keeps working over REST (hub optional), so IT and the employee can still coordinate — including the system message explaining that the connection could not be established.

## Permissions

`remote.request`, `remote.attended`, `remote.unattended`, `remote.audit.read`, `remote.admin` (mapping)

## MVP

Attended + records + adapter to MeshCentral (or mock). Broad unattended is post-MVP unless a single admin role is explicitly accepted in MVP-DEFINITION.
