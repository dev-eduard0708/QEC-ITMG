# ADR-0010: Authentication strategy

Date: 2026-09-02
Status: Accepted

## Context

QEC already has Microsoft identity. Privileged IT actions need MFA. The app must still authorize internally.

## Decision

- **Primary authentication:** Microsoft Entra ID (or AD FS / OIDC-capable AD) via OpenID Connect
- **Authorization:** application RBAC (permissions in SQL), not “Domain Admins can do everything in ITMG”
- **MFA:** required for privileged permission use (Entra Conditional Access and/or step-up)
- **Break-glass:** few local accounts, monitored, not used daily
- **Future:** service identities for APIs/integrations (OAuth client credentials / managed certificates)

## Rationale

- Users should not have a second password store as the happy path
- IdP groups may **suggest** roles; Platform Administrator still assigns ITMG roles
- Remote unattended and role admin must not rely on MeshCentral login

## Consequences

- Staging needs a test tenant or equivalent
- Group mapping is many-to-many and audited

## Alternatives considered

- SQL-only passwords for all users: rejected as primary
- Windows Integrated Auth only: fragile for SPA + reverse proxy + MFA
