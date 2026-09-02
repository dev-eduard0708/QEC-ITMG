# Configuration

ASP.NET configuration:

- Connection strings
- File root path
- OIDC client id/secret (secret store)
- SMTP
- Engine base URL + service credential
- Hangfire
- Feature flags (unattended, modules)

Never commit secrets. `appsettings.json` has non-secret defaults only.

Feature flags hide **UI and API** for modules not yet released.
