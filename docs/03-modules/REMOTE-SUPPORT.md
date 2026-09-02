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

The engine **must not** be a hidden bypass. Operational control: technicians are not Domain Admins on MeshCentral; only the app’s service account creates sessions. Engine UI is break-glass.

## Mapping

`ConfigurationItem.RemoteEngineNodeId` (or mapping table). No session without a CI (create CI first).

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

If engine is down: request/consent still persist; connect button fails clearly. Audit still valid.

## Permissions

`remote.request`, `remote.attended`, `remote.unattended`, `remote.audit.read`, `remote.admin` (mapping)

## MVP

Attended + records + adapter to MeshCentral (or mock). Broad unattended is post-MVP unless a single admin role is explicitly accepted in MVP-DEFINITION.
