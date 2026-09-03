# ADR-0010: Authentication strategy

Date: 2026-09-02
Status: Accepted (amended 2026-09-03 — primary IdP pivoted to Google OIDC)

## Context

QEC users authenticate with Google Workspace. Privileged IT actions need MFA. The app must still authorize internally.

## Decision

- **Primary authentication:** Google OpenID Connect (`https://accounts.google.com`) via BFF cookie session
- **Authorization:** application RBAC (permissions in SQL), not Google groups or IdP roles
- **Identity claims:** `sub` → external id, `email` → UPN, `name` → display name; `email_verified` required
- **Domain restriction:** configurable `Authentication:Oidc:AllowedDomains` (empty allowed in Development)
- **MFA:** Google Workspace / IdP MFA and/or app step-up for privileged permissions
- **Break-glass:** few local accounts, monitored, not used daily
- **Future:** service identities for APIs/integrations (OAuth client credentials / managed certificates)

## Rationale

- Users should not have a second password store as the happy path
- IdP groups must **not** grant ITMG permissions; Platform Administrator assigns ITMG roles in SQL
- Remote unattended and role admin must not rely on MeshCentral login

## Consequences

- Staging/Production need Google OAuth client credentials and (for prod) allowed Workspace domains
- Microsoft Entra-specific claims (`oid`, `preferred_username`, objectidentifier) are not used at runtime

## Alternatives considered

- Microsoft Entra ID as primary OIDC provider: superseded for current QEC deployment
- SQL-only passwords for all users: rejected as primary
- Windows Integrated Auth only: fragile for SPA + reverse proxy + MFA
