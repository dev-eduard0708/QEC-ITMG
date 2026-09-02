# Security architecture

Related: [AUTHENTICATION.md](AUTHENTICATION.md) · [AUTHORIZATION-RBAC.md](AUTHORIZATION-RBAC.md) · [THREAT-MODEL.md](THREAT-MODEL.md) · [SECURITY-CHECKLIST.md](SECURITY-CHECKLIST.md)

## Objective

Protect confidentiality, integrity, and availability of QEC operational and GRC data. Treat **remote support** and **evidence export** as highly privileged.

## Control themes

| Theme | Approach |
|-------|----------|
| Authentication | Entra ID / AD OIDC; MFA for privileged; break-glass |
| Authorization | Permission keys + resource checks; SoD on approve vs implement |
| Session | Short idle timeout for privileged; server-side revoke |
| CSRF | SameSite cookies + antiforgery if cookie auth |
| XSS | CSP, React escaping, no `dangerouslySetInnerHTML` for ticket HTML unless sanitized |
| SQL injection | EF parameterized; no concatenated SQL |
| IDOR/BOLA | Every GET/PUT checks resource scope; tests |
| Rate limiting | Proxy + ASP.NET rate limiter on auth and upload |
| Uploads | Allowlist, size, malware state machine |
| Secrets | Config/vault; never logs |
| Encryption | TLS in transit; TDE/volume at rest; HTTPS only |
| Audit | Business history + security log |
| Classification | Field and attachment labels; UI/API filtering |
| Service accounts | Named, owned, reviewed |
| Jobs | Hangfire as service identity; no RBAC bypass |
| SDLC | PR review, dependency scanning, architecture tests |

## Privileged actions (always MFA + audit)

Role admin, unattended remote, evidence export, break-glass, integration secret view, mass user disable, workflow definition change.

## Remote support

See [REMOTE-ACCESS-SECURITY.md](REMOTE-ACCESS-SECURITY.md). Engine isolation is mandatory.

## Logging sensitivity

Do not log ticket descriptions if they may contain passwords users paste. Truncate; mark Restricted comments. Redact Authorization headers.

## Backup and DR of the platform

[../10-operations/BACKUP-RESTORE.md](../10-operations/BACKUP-RESTORE.md), [../10-operations/DR-PLAN-FOR-PLATFORM.md](../10-operations/DR-PLAN-FOR-PLATFORM.md).

## Secure development

No production secrets in repo. Dependency updates. Threat model updates when adding integrations. Security tests in [../09-testing/SECURITY-TESTING.md](../09-testing/SECURITY-TESTING.md).
