# Authentication

Related: [ADR-0010](../12-decisions/ADR-0010-authentication.md) · [AUTHORIZATION-RBAC.md](AUTHORIZATION-RBAC.md)

## Primary path

1. User hits SPA → redirected to **Google OIDC** (`https://accounts.google.com`)
2. Authorization code + PKCE via **BFF**: ASP.NET holds the session cookie; SPA is same-site and calls same-origin `/api` (no access token in localStorage)
3. Cookie session remains the application session after OIDC completes

## Claim mapping (Google)

| Google claim | ITMG use |
| --- | --- |
| `sub` | `qec_external_id` (stable external id) |
| `email` | UPN / login email |
| `name` | Display name |
| `email_verified` | Must be `true` or sign-in fails |

Google groups / IdP role claims **never** grant ITMG permissions. Authorization is SQL RBAC only.

Optional allow-list: `Authentication:Oidc:AllowedDomains`. Empty list allows any verified Google account (typical Development). Production should restrict to the QEC Google Workspace domain(s).

## Session

- HTTP-only, Secure, SameSite=Lax (or Strict if it does not break OIDC return)
- Sliding expiration with hard cap
- Privileged permission use may require recent MFA / step-up (IdP or app claim)

## MFA

Google Workspace / IdP MFA for IT and admin accounts. Application additionally checks `remote.unattended` and `admin.roles` for step-up claim.

## JIT provisioning

On first login: create User from `sub` + email UPN. Assign Employee. IT roles assigned in Administration (not from Google groups).

## Break-glass

Emergency local login independent of Google, disabled by default:

- Config: `Authentication:BreakGlass` (`Enabled`, `Accounts[]` with `Username`, `UserUpn`, `PasswordHash`)
- Endpoint: `POST /auth/break-glass`
- SPA route: `/break-glass`
- Password hashes use ASP.NET Identity `PasswordHasher` and must live in secrets / `appsettings.*.local.json` — never commit real hashes
- Maps to an existing **Active** ITMG user by `UserUpn`; does **not** grant admin permissions
- Authorization remains SQL RBAC
- Audit: `BreakGlassLoginSuccess` / `BreakGlassLoginFailed` (no password or hash logging)

## API / future services

Client credentials or certificate for adapters. Not user passwords.

## Logout

Revoke app session; optionally Google / OIDC end-session.
