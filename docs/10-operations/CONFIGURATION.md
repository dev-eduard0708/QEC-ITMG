# Configuration

ASP.NET configuration:

- Connection strings
- File root path
- OIDC (Google): Authority, ClientId/ClientSecret (secret store), AllowedDomains
- Break-glass: `Authentication:BreakGlass` accounts + password hashes (secret store / local overrides only)
- SMTP (`Email:Smtp` — Development defaults target Mailpit on localhost:1025; UI on :8025)
- Engine base URL + service credential
- Hangfire
- Feature flags (unattended, modules)

Never commit secrets. `appsettings.json` has non-secret defaults only.

Feature flags hide **UI and API** for modules not yet released.
