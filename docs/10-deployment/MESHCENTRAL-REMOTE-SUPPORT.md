# MeshCentral remote support deployment

Related: [REMOTE-SUPPORT.md](../03-modules/REMOTE-SUPPORT.md) · [ADR-0014](../12-decisions/ADR-0014-meshcentral-default-engine.md)

## What ITMG implements vs what MeshCentral provides

| ITMG (this repo) | MeshCentral (external host) |
|------------------|-----------------------------|
| Identity, RBAC, consent, chat, audit | Screen/input transport |
| On-demand `RemoteEndpoint` + one-time enrollment | Agent binaries + node presence |
| Session-bound Support Helper package | Device group (`meshid`) |
| `control.ashx` WebSocket client (MeshCtrl-compatible) | Control channel / desktop viewer |

**Important:** ITMG does **not** call invented REST paths such as `api/mesh/sessions`.  
Session “Connect” verifies the node is online via MeshCentral `nodes`, then returns a real desktop join URL:

`{BaseUrl}/?viewmode=11&gotonode={nodeId}`

## Operator checklist

1. **Provision MeshCentral** on a dedicated host with TLS and a public DNS name reachable by employees anywhere in Saudi Arabia (no same-LAN assumption).
2. **Create a restricted service user** (not a broad Domain Admin). Prefer a dedicated account with rights only on the ITMG support device group.
3. **Create a device group** for on-demand support. Copy its Mesh id (MeshCtrl `ListDeviceGroups` / UI group id).
4. **Store credentials** in the secret store referenced by `RemoteSupport:CredentialReference`. Supported formats:
   - `username:password`
   - JSON `{ "username": "...", "password": "..." }`
5. **Configure ITMG** (`appsettings` / environment — never commit secrets):

```json
"RemoteSupport": {
  "Enabled": true,
  "ProviderKind": "MeshCentral",
  "BaseUrl": "https://mesh.example.qehc.edu.sa",
  "CredentialReference": "secret://remote-support/meshcentral",
  "WebhookSignatureReference": "",
  "UnattendedEnabled": false,
  "MeshDeviceGroupId": "mesh//domain/....",
  "WindowsAgentTypeId": 4,
  "PublicAppBaseUrl": "https://itmg.example.qehc.edu.sa",
  "HelperArtifactPath": "C:\\\\deploy\\\\remote-support",
  "EnrollmentTokenLifetimeMinutes": 10,
  "TemporaryEndpointRetentionHours": 72,
  "AllowDevelopmentMockEnrollment": false
}
```

6. **Publish Support Helper** (self-contained win-x64):

```powershell
./scripts/publish-remote-support-helper.ps1
```

Output lands in `artifacts/remote-support/` (gitignored). Point `HelperArtifactPath` at that folder or `QecRemoteSupportHelper.exe`.

7. **Verify engine health** on IT Remote Support readiness (Configured/Healthy, agent enrollment available, helper artifact available).
8. **Attended test:** Employee Get Remote Help → Download Support Helper → agent installs → Support sees Ready → Request Remote Access → Allow → Connect (opens MeshCentral desktop URL).

## Firewall / network

- Employees need HTTPS to ITMG and HTTPS/WSS to MeshCentral (agent + browser viewer).
- Support staff browsers need HTTPS to MeshCentral for the desktop join URL.
- ITMG server needs outbound HTTPS/WSS to MeshCentral `control.ashx`.

## Agent download

When `MeshDeviceGroupId` is set, ITMG builds the native MeshCentral agent URL:

`{BaseUrl}/meshagents?id={WindowsAgentTypeId}&meshid={MeshDeviceGroupId}`

(Windows agent type `4` is the common x64 agent used by MeshCtrl `AgentDownload`.)

## Development without MeshCentral

Leave `Enabled=false` or omit credentials. Chat, enrollment, and endpoint registration still work. UI shows Waiting for remote agent / engine not configured — never false Ready.
