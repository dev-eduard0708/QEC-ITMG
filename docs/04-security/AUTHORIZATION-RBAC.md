# Authorization and RBAC

Related: [../03-modules/ADMINISTRATION.md](../03-modules/ADMINISTRATION.md)

## Model

```
User 1—n UserRole n—1 Role 1—n RolePermission n—1 Permission
```

Permission name: `resource.action` (and optional `.qualifier`).

Roles **group** permissions. Never `if (role == "IT Manager")` in use cases. Check permission + resource.

## Seed roles (composition examples)

| Role | Intent |
|------|--------|
| Employee | Own tickets, own assets, KB published, attended consent |
| Help Desk Agent | Queue tickets, no unattended, no change approve |
| IT Technician | Tickets, assets read, attended remote, changes create |
| Senior IT Technician | + assign, broader queues |
| Network Engineer | Network CIs, related changes |
| Systems Administrator | Servers, unattended if granted separately |
| Application Administrator | App CIs, access fulfill for those apps |
| Cybersecurity Analyst | Security incidents, vulns, risks |
| Compliance Officer | Controls, assessments, policies |
| Auditor | Read audit/evidence/controls; no manage |
| Read-Only Auditor | Same, stricter export |
| Change Manager | Change process |
| Change Approver | `change.approve` only |
| Asset Manager | Asset lifecycle |
| Service Desk Manager | SLA, queues, reports |
| IT Manager | Operational reports, approvals |
| Platform Administrator | Admin settings, not automatically unattended remote |

Unattended remote is a **permission**, added only to roles that need it.

## Resource-level rules

- Ticket: requester, watcher, assignee, queue membership, or `ticket.read.all`
- Security ticket: `ticket.read.security`
- CI Confidential: classification gate
- Evidence Restricted: `evidence.read.restricted`
- Remote session: technician of record, user of device, or `remote.audit.read`

## SoD

Cannot sole-approve own normal change. Cannot complete own access review for privileged self. Configurable.

## API

Every endpoint has a policy. 403 vs 404: use 404 for existence hiding on Restricted resources.
