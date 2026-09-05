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
| On-demand `RemoteEndpoint` + enrollment | Agent package / MeshCentral node |

The engine **must not** be a hidden bypass. Operational control: technicians are not Domain Admins on MeshCentral; only the app’s service account creates sessions. Engine UI is break-glass.

## Identity-first eligibility

Remote Support eligibility is based on **authenticated ITMG identity**, not raw email suffixes and not browser-supplied addresses.

Employee self-service requires:

- authenticated BFF/cookie session
- Active ITMG user
- `UserType = Employee` (or Support/Admin permissions)
- permission `remote.self.request`

Production company-domain enforcement stays in Google OIDC:

`Authentication:Oidc:AllowedDomains`

Development may continue to allow configured personal-Gmail test accounts. Remote Support does **not** hardcode `qehc.edu.sa` and does **not** re-check email domains.

SQL RBAC remains authoritative for technician powers (`remote.request`, `remote.attended`, …). Google group/domain membership alone never grants Support privileges.

## Two device modes

### Managed device

Employee or IT selects a company CI with `ConfigurationItem.RemoteEngineNodeId` mapping. Helper download can be skipped when the mapping is healthy.

### On-demand / temporary device

Employee may request help from a computer that is **not** in Asset Management or CMDB (home PC, branch workstation, personal laptop used for authorized work, etc.).

Flow:

1. Authenticated Active Employee → **Get Remote Help**
2. Chat opens immediately (ticket auto-created/linked)
3. **Prepare this computer** → one-time enrollment token (hash stored only)
4. Support Helper redeems token → `RemoteEndpoint` (Temporary) appears to Support
5. Technician chats → **Request remote access** → employee **Allow**
6. MeshCentral connects using resolved engine node when available
7. Session ends → temporary association expires; unattended remains prohibited

Managed devices use CI mapping. On-demand attended support may use a request-scoped `RemoteEndpoint`. A CI is **not** required for on-demand mode. Temporary endpoints are never auto-promoted to permanent assets; `remote.admin` may link to an existing CI later.

Location is independent: no same-LAN / same-branch / known corporate IP / VPN requirement is imposed by the application.

## RemoteEndpoint

Lightweight RemoteSupport-owned identity for operation only — **not** an Asset and **not** a CMDB replacement.

Minimum fields: device name, OS, architecture, helper/agent versions, connection status, optional engine node, optional CI link.

Do not collect serial numbers, software inventory, personal files, browser history, GPS, or unnecessary MACs by default.

## Mapping (managed)

`ConfigurationItem.RemoteEngineNodeId` remains the managed-device mapping. ITMG CI is the operational reference; external Asset Management remains authoritative for physical lifecycle.

## Employee Get Remote Help

`POST /api/v1/me/remote-support` (`remote.self.request`):

- creates attended `RemoteSessionRequest` (`Status=Requested`, technician unassigned)
- auto-creates a Service Request ticket and links it
- optional managed `ConfigurationItemId`
- opens chat immediately

Enrollment: `POST /api/v1/me/remote-support/{id}/enrollment` issues a ≥256-bit single-use token (10 minutes default). Only the SHA-256 hash is stored. Helper redeems via `POST /api/v1/remote-support/enrollments/redeem` (no browser cookie). Tokens are never logged or BusinessAudited in plaintext.

Helper source: `tools/Qec.Itmg.RemoteSupport.Helper` (build/sign outside git — **no EXE/MSI in the repo**). Configure `RemoteSupport:HelperDownloadUrl` for employee download. If unset: “Support Helper is not available on this environment.”

MeshCentral automatic node provisioning is **deferred** unless a real documented API is configured (`IRemoteEndpointEnrollmentEngine`). Endpoints may register with ITMG first and show “Waiting for remote agent” without falsely marking Ready.

## Session chat

Preserved from V1. Chat works **before** helper install, device registration, consent, and engine connectivity.

System events include self-request, technician joined, enrollment, device registered/ready, access requested, consent, connect, end.

Chat is **not** consent.

## Support queue

IT `/it/remote-support` shows waiting/assigned/device/consent/connect states. Technicians **Take request** (`AssignTechnician`). Endpoints admin list: `/it/remote-support/endpoints`.

## Attended connect

1. Device Ready (managed mapping or temporary endpoint with engine node)
2. Technician **Request remote access** → employee consent banner
3. Employee **Allow remote access** / **Decline**
4. Technician **Connect** (engine available, consent Allowed, target Ready)

## Unattended

Unchanged policy. Temporary endpoints **cannot** be unattended. Employee UI never exposes unattended.

## Permissions

`remote.self.request`, `remote.request`, `remote.attended`, `remote.unattended`, `remote.audit.read`, `remote.admin`

Default Employee role is seeded with `remote.self.request` only.

## Degraded mode

If engine is down: request/consent/chat still work; Connect fails clearly. Device may be detected while remote agent is still preparing.
