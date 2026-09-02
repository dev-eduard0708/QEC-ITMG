# Administration

Related: [../04-security/AUTHORIZATION-RBAC.md](../04-security/AUTHORIZATION-RBAC.md) · [../10-operations/CONFIGURATION.md](../10-operations/CONFIGURATION.md)

## Purpose

Users, roles, permissions, departments, locations, categories, SLA configuration, workflow definitions, notification templates, integration settings, system settings.

## Rules

- Role names are not authorization. Permissions are.
- Changes to roles/permissions are business + security audit events.
- Lookups (ticket categories, CI types, prefixes) are data.
- System settings: timezone default, file size limits, session timeout, engine adapter config **references**.

## Users

Provisioned from SSO first login (JIT) with default **Employee** role, or pre-provisioned. Disable follows directory when integration exists.

## Permissions

`admin.users`, `admin.roles`, `admin.settings`, `admin.integrations`, `admin.lookups`

Platform Administrator is a role **composed of** these permissions, plus break-glass procedures.

## Workflows

Admin UI for states/transitions of allowed workflow types — not a general BPM designer in MVP.
