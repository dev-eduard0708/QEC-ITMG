# Threat model (STRIDE-oriented)

| Asset | Threat | Mitigation |
|-------|--------|------------|
| Sessions | Token theft (XSS) | BFF cookies, CSP, HttpOnly |
| Tickets | IDOR | Resource authz tests |
| Remote | Unauthorized desktop | ITMG gate, engine lock down |
| Remote | Malicious technician | Identity, reason, recording optional, SoD |
| Files | Malware upload | Scan state, allowlist |
| Files | Path traversal | Opaque keys |
| SQL | Injection | EF, no raw concat |
| SQL | Backup theft | Encrypt, restrict DBA ops |
| API | Mass assign role | Separate endpoints, admin permission |
| API | CSRF | SameSite + antiforgery |
| Jobs | Privilege via Hangfire | Restricted dashboard, no user job injection |
| Webhook | Forged session end/start | HMAC, never start from webhook |
| Insider | Silent evidence delete | Soft delete + history, no UI hard delete |
| Engine | Admin bypass | Process/network controls |
| SPA | Hidden button only | Server enforcement |
| Reports | Data scrape | Rate limit, permission, audit export |
| MFA | Skip on unattended | Step-up required |

Trust boundary diagram: [../01-architecture/SYSTEM-ARCHITECTURE.md](../01-architecture/SYSTEM-ARCHITECTURE.md).

Update this file when adding integrations (Phase 19) or AI tools (Phase 20 — prompt injection, data exfil via tools).
