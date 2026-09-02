# Integration architecture

Related: [../03-modules/REMOTE-SUPPORT.md](../03-modules/REMOTE-SUPPORT.md) · [ADR-0008](../12-decisions/ADR-0008-remote-support-integration.md) · [../11-planning/MASTER-ROADMAP.md](../11-planning/MASTER-ROADMAP.md)

Integrations are **not implemented** in the documentation phase. This document defines boundaries.

## Pattern

Each external system has an **adapter** behind an application interface.

```
Use case → IRemoteSupportEngine / IMailSender / IIdentityDirectory
                ↓
         Adapter (HTTP/SDK)
                ↓
         External system
```

Adapters:

- Map external errors to platform results
- Never persist “engine admin = authorized”
- Log with correlation id; redact secrets
- Are disabled by feature flags per environment

## Identity

| System | Direction | Purpose |
|--------|-----------|---------|
| Microsoft Entra ID | Inbound OIDC | Authentication |
| Active Directory | Inbound LDAP/GC or Entra sync | Groups, disablement later for JML |
| HR system | Inbound (future) | Joiner/mover/leaver triggers |

QEC ITMG stores **authorization** (roles/permissions). IdP stores **authentication**.

## Collaboration

| System | Direction | Purpose |
|--------|-----------|---------|
| SMTP | Outbound | Email notifications |
| Microsoft 365 | Future | Mailbox/calendar actions for leaver, optional |
| Microsoft Teams | Future | Notification channel |

## Remote support

See remote-support module. ITMG → engine: create/join session, list agent online, optionally record metadata. Engine → ITMG: webhooks for session end if available; otherwise polling job.

## Operations / security telemetry (future)

| System | Direction | Purpose |
|--------|-----------|---------|
| Veeam | Inbound | Backup job success/fail as Events |
| VMware / Hyper-V | Inbound | Inventory enrichment for CIs |
| Network monitoring / SNMP | Inbound | Events (normalized, not raw) |
| Firewalls | Inbound | Change/event hints — careful, high volume |
| Vulnerability scanners | Inbound | Vulnerability findings |
| Endpoint security | Inbound | Device health / incidents |
| Certificate sources | Inbound | Certificate inventory |
| Windows event collection | Generally out — SIEM | Optional alerts as Events |
| SIEM | Outbound | Security audit log stream |

## Business systems (future)

| System | Direction | Purpose |
|--------|-----------|---------|
| Procurement | Inbound | Asset purchase |
| Discovery / agent inventory | Inbound | Hostname, serial enrichment |

## AI platform (future)

Outbound prompts with **redaction** and **user-bound tokens**. Must call QEC ITMG APIs as the user (or a constrained service that re-checks RBAC per tool). See Phase 20.

## Webhooks inbound

- Authenticated (HMAC or mTLS)
- Idempotency keys
- Never grant unattended remote or role changes from a webhook alone

## Credential store

Integration secrets in configuration/vault. Table `adm.IntegrationCredential` stores **references and metadata**, not raw passwords if a vault exists. If vault is unavailable in v1, encrypted-at-rest secrets with strict ACL and audit on read.
