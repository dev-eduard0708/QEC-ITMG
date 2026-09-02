# Authentication

Related: [ADR-0010](../12-decisions/ADR-0010-authentication.md) · [AUTHORIZATION-RBAC.md](AUTHORIZATION-RBAC.md)

## Primary path

1. User hits SPA → redirected to Entra ID / AD OIDC
2. Authorization code + PKCE (SPA) **or** BFF pattern (preferred): ASP.NET holds session cookie, SPA is same-site, no access token in localStorage
3. **Decision: Backend-for-frontend cookie session** to reduce token theft. SPA calls same origin `/api`.

## Session

- HTTP-only, Secure, SameSite=Lax (or Strict if it does not break OIDC return)
- Sliding expiration with hard cap
- Privileged permission use may require recent MFA (Entra ACR or re-auth)

## MFA

Conditional Access for IT and admin groups. Application additionally checks `remote.unattended` and `admin.roles` for step-up claim.

## JIT provisioning

On first login: create User from `oid`/`sub` + UPN. Assign Employee. IT roles assigned in Administration, optionally suggested from IdP groups (audited mapping).

## Break-glass

Local identity (ASP.NET Identity) **disabled** except two named accounts in secrets, IP restricted if possible, alert on use.

## API / future services

Client credentials or certificate for adapters. Not user passwords.

## Logout

Revoke app session; optionally Entra logout.
