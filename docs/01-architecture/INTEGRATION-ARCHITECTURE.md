# Integration architecture

Related: [SYSTEM-ARCHITECTURE.md](SYSTEM-ARCHITECTURE.md) · [../10-operations/CONFIGURATION.md](../10-operations/CONFIGURATION.md)

## Status: readiness stubs only

Three vendor integrations are defined as **disabled adapters**.
No outbound vendor connections exist. Production integration requires explicit **QEC authorization**.

## Approved vendors (disabled)

| Vendor | Interface | Config prefix |
|--------|-----------|---------------|
| Veeam Backup & Replication / Enterprise Manager | `IVeeamClient` | `Integrations:Veeam` |
| SonicWall Capture Client | `ISonicWallCaptureClient` | `Integrations:SonicWallCaptureClient` |
| Synology DSM | `ISynologyMonitor` | `Integrations:Synology` |

All three default to `Enabled: false` and `RuntimeMode: Disabled`.

## Configuration

```json
"Integrations": {
  "Veeam":                 { "Enabled": false, "BaseUrl": "", "CredentialReference": "" },
  "SonicWallCaptureClient": { "Enabled": false, "BaseUrl": "", "CredentialReference": "" },
  "Synology":              { "Enabled": false, "BaseUrl": "", "CredentialReference": "" }
}
```

- `CredentialReference` is a **secret-store reference name only** — never an actual API key, token, username, or password.
- `Configured` becomes `true` only when `Enabled`, `BaseUrl`, and `CredentialReference` are all non-empty.

## Contracts

Interfaces and snapshot DTOs live under `src/Qec.Itmg.Contracts/Integrations/`.
Read-only data retrieval only. No write, remote-control, or scan/remediation commands are defined.

## Readiness API

`GET /api/v1/admin/integrations/readiness` — requires `admin.integrations` permission.

Returns configuration/readiness state. **Never contacts any vendor system.**

## Road map

| Phase | Work |
|-------|------|
| P2-03 | Generic `IMalwareScanner` abstraction for attachment scanning — SonicWall Capture Client is **not** the scanner |
| P3 | Map external devices and workloads to CMDB entities |
| P8 | Consume Veeam / Synology backup and replication operational data |
| P15 | Consume SonicWall Capture Client endpoint security and detection data |
| P19 | Implement real vendor adapters after production authorization is granted |

## Inbound integrations (existing)

| System | Direction | Purpose |
|--------|-----------|---------|
| Google OIDC | Inbound | Authentication (primary) |
| SMTP / Mailpit | Outbound | Email notifications (future) |
