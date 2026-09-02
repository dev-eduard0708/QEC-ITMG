# Change management

Related: [../02-domain/STATUS-MODELS.md](../02-domain/STATUS-MODELS.md) · [ASSET-CMDB.md](ASSET-CMDB.md) · [ACCESS-MANAGEMENT.md](ACCESS-MANAGEMENT.md)

## Purpose

Controlled modification of services/CIs with assessment, approval, implementation evidence, and PIR where required.

## Types

| Type | Path |
|------|------|
| Standard | Catalog item, pre-authorized, still logged, still linked to CIs |
| Normal | Full assessment and approval |
| Emergency | Shortened approval (defined emergency approver), mandatory PIR, no silent close |

Unauthorized production changes discovered later are recorded as `ChangeRequest` with `IsRetrospective = true` and flagged in metrics (unauthorized change rate).

## Required content

- Affected CIs (minimum one for implementation)
- Business, technical, security impact
- Risk rating
- Implementation steps
- Test plan
- Rollback plan
- Schedule window
- Validation evidence (attachments)
- Result: success / fail / rollback
- PIR for normal (major) and all emergency

## Approvals

Workflow engine: Change Manager / CAB role / CI owner as configured. SoD: requester cannot be sole approver for normal/emergency (`change.approve` + not same user), except documented emergency break with dual control after the fact.

## Implementation

`change.implement` distinct from approve. Remote unattended to production CIs **should** reference the change or incident.

## Permissions

`change.create`, `change.read`, `change.assess`, `change.approve`, `change.schedule`, `change.implement`, `change.pir`, `change.catalog.manage`
