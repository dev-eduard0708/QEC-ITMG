# Integration architecture

Related: [SYSTEM-ARCHITECTURE.md](SYSTEM-ARCHITECTURE.md) · [../10-operations/CONFIGURATION.md](../10-operations/CONFIGURATION.md)

## Status: real adapters implemented (disabled by default)

Vendor and platform integrations ship as **real adapter code** behind contracts.
Runtime defaults to **Disabled**. Activation requires explicit QEC configuration and authorization.
No live vendor connectivity is assumed in development.

## Providers

| Provider | Interface | Config prefix |
|----------|-----------|---------------|
| Directory (Graph/LDAP style) | `IDirectorySyncClient` | `Integrations:Directory` |
| Mail / M365 Graph | `IEmailSender` via `ConfigurableEmailSender` | `Integrations:Mail` (+ SMTP default) |
| Veeam | `IVeeamClient` | `Integrations:Veeam` |
| Synology DSM | `ISynologyMonitor` | `Integrations:Synology` |
| SonicWall Capture Client | `ISonicWallCaptureClient` | `Integrations:SonicWallCaptureClient` |
| Virtualization (vCenter/Hyper-V) | `IVirtualizationEnrichmentClient` | `Integrations:Virtualization` |
| Vulnerability scanner | `IVulnerabilityScannerIngestClient` | `Integrations:VulnerabilityScanner` |
| SIEM outbound | `ISiemPublisher` | `Integrations:Siem` |
| Inbound webhooks | `IIntegrationWebhookProcessor` | `Integrations:Webhook` |

## Configuration

```json
"Integrations": {
  "Veeam": { "Enabled": false, "BaseUrl": "", "CredentialReference": "" },
  "Directory": { "Enabled": false, "BaseUrl": "", "CredentialReference": "", "ProviderKind": "Graph" },
  "Mail": { "Enabled": false, "BaseUrl": "https://graph.microsoft.com", "CredentialReference": "", "ProviderKind": "Graph", "MailboxAddress": "" },
  "Siem": { "Enabled": false, "BaseUrl": "", "CredentialReference": "" },
  "Webhook": { "Enabled": false, "CredentialReference": "", "RequiresBaseUrl": false, "WebhookSignatureReference": "" }
}
```

- `CredentialReference` is a **secret-store reference name only** — never an API key, token, username, or password.
- Secrets resolve via `ISecretResolver` from environment `ITMG_SECRET_{REFERENCE}` or configuration `Secrets:{REFERENCE}` (user-secrets/env for development; pluggable store for production).
- Readiness statuses: **Disabled / NotConfigured / Configured / Healthy / Unhealthy**.
- Healthy/Unhealthy are set only after real runtime sync attempts.

## Secrets policy

Never store passwords, API keys, client secrets, access tokens, or refresh tokens in:

- `appsettings*.json`
- database tables
- Git
- audit/integration logs
- admin UI

## Operations

- Hangfire job `integration-polling` syncs **enabled** providers hourly (skips disabled; overlap-safe).
- Admin API/UI: `/api/v1/admin/integrations/*` and `/it/admin/integrations` (`admin.integrations`).
- Sync now never silently enables a provider.
- JML directory actions require AccessCase in **Fulfillment**; approvals/SoD remain authoritative.
- Webhooks require HMAC signature, timestamp freshness, idempotency, allowlist, and payload size limits.

## Persistence (`plt`)

- `IntegrationRun` — sync history/counts
- `IntegrationWebhookReceipt` — inbound idempotency (payload hash only)
- `IntegrationCorrelation` — external ID ↔ CI/user/finding correlation (including unmatched review)

## Inbound integrations (existing)

| System | Direction | Purpose |
|--------|-----------|---------|
| Google OIDC | Inbound | Authentication (primary) |
| SMTP / Mailpit | Outbound | Default email notifications |
| Hardened webhooks | Inbound | Provider events (`POST /api/v1/integrations/webhooks/{provider}`) |
