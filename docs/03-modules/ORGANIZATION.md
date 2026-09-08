# Organization

Related: [ADMINISTRATION.md](ADMINISTRATION.md) · [ACCESS-MANAGEMENT.md](ACCESS-MANAGEMENT.md) · [../04-security/AUTHORIZATION-RBAC.md](../04-security/AUTHORIZATION-RBAC.md) · [../04-security/AUTHENTICATION.md](../04-security/AUTHENTICATION.md) · [../11-planning/ORGANIZATION-EXPANSION-PLAN.md](../11-planning/ORGANIZATION-EXPANSION-PLAN.md)

## Purpose

Organization holds structure for QEC: **Organizational Units** (persisted as `Department` rows — table name unchanged), **Department membership**, **Locations**, and the **Position hierarchy** (reporting structure of roles/posts per department).

This organizational reference data is reusable for future targeting (for example policy or security-awareness assignments by company, department, child department, position, or employee). Those workflows are **not** implemented here.

## Current rollout scope — QEC Head Office

**The first production scope of QEC ITMG Organization is QEC Head Office only.**

The Head Office hierarchy is HR-aligned around a company root and functional departments:

```
QEC (Company)
├─ Executive Management (EXEC)
├─ Human Resources (HR)
│  └─ HR sections (Payroll, Admin & Support, …)
├─ Finance (FINANCE)
├─ Information Technology (IT)
├─ Legal Affairs (LEGAL)
├─ PMO (PM)
│  └─ PM Staff / Operations / PM / PM Office
└─ Marketing (MARKETING)
```

The exact hierarchy remains administrator-configurable. `Department.UnitType` classifies each unit as Company, Department, Section, Office, Team, or Other.

For the current phase:

- **Departments** (OU rows) represent Head Office functional / administrative units.
- **Positions** represent Head Office positions/posts.
- **DepartmentMembership** organizes Head Office employees.
- **PositionAssignment** assigns employees to Head Office positions.
- **Google** provides identity / name / email / avatar.
- **ITMG** provides organizational placement.
- Department / Position **never** automatically grant RBAC.
- Access Management routing remains separate (selects users, not positions).

This phase does **not** claim that all QEC project, school, branch, academy, site, or field employees are already modeled. Future expansion to projects/sites is documented in [ORGANIZATION-EXPANSION-PLAN.md](../11-planning/ORGANIZATION-EXPANSION-PLAN.md) and is **not implemented**. Unit types such as Project / School / Branch / Site / Academy are intentionally **not** part of the Head Office `DepartmentUnitType` enum.

### Security Awareness V1 (planned — not implemented here)

Planned Security Awareness V1 will initially target **Head Office employees only**.

See [SECURITY-AWARENESS.md](SECURITY-AWARENESS.md) for the implemented Head Office awareness audience model, campaign builder, and employee My Awareness experience. Project/site targeting and phishing simulation remain deferred per [ORGANIZATION-EXPANSION-PLAN.md](../11-planning/ORGANIZATION-EXPANSION-PLAN.md).

Examples of intended V1 audiences:

- All Head Office Employees
- Executive Management
- PMO / Project Management
- Human Resources
- Finance
- Information Technology
- Head Office positions
- Specific Head Office employees

For V1, **“All Employees”** inside Security Awareness means all employees within the currently enabled **Head Office** awareness scope — **not** automatically every employee working at every QEC project/site. Future organization expansion will extend awareness targeting. Awareness department pickers continue to use `listDepartments` (all active units).

## Identity ownership split

| Concern | Owner |
| --- | --- |
| Display name, email/UPN, profile avatar URL | **Google** (OIDC claims; synced into Identity `User` on login) |
| Departments / OUs, membership, positions, hierarchy | **QEC ITMG** (Organization module) |
| RBAC permissions / roles | **QEC ITMG** (Identity) — independent of org structure |
| Access case routing | **Access Management** — selects users, not positions |

Google Admin Directory API is **not** used. Department/job-title claims from Google are **not** trusted for org placement.

## Starter company structure

`Department.ParentDepartmentId` represents cross-unit organizational reporting (Company View tree). `Position.ParentPositionId` remains **department-scoped** and cannot point across departments.

Idempotent seed (`OrganizationPositionsSeed`):

- Creates **QEC** company root when missing.
- Conservatively aligns known starter nodes (EXEC / HR / FINANCE / IT / PM) — renames/reparents only when still on known starter names or known starter parent patterns (e.g. still under EXEC). Admin-customized names/parents are not overwritten.
- Adds LEGAL, MARKETING, HR sections, and PM child units when missing.
- Attaches orphan starter departments to **QEC** (not EXEC).
- Preserves existing positions on EXEC / IT / PM; adds `HR_MANAGER` under HR when missing.
- Does **not** seed user assignments or an “HR Manager” organizational unit.

### Executive positions

```
EXEC-CEO (Chief Executive Officer / الرئيس التنفيذي)
├── EXEC-CEO-SECRETARY (CEO Executive Secretary / السكرتير التنفيذي للرئيس التنفيذي)
└── EXEC-VP (Vice President / نائب الرئيس)
    └── EXEC-VP-SECRETARY (VP Executive Secretary / السكرتير التنفيذي لنائب الرئيس)
```

### Project Management (PMO) positions

Positions remain on department code **PM** (PMO):

```
PM-HEAD → PM-PROJECT-MANAGER → coordinators; PM-DEVELOPMENT-COORDINATOR under PM-HEAD
```

### Project Management → HR responsibility boundary

Informational organizational responsibility only (no ATS / onboarding workflow in this module):

Project opportunity → delivery → staffing requirements → candidate identification/selection → **endorse to Human Resources** → HR formal employment / onboarding.

## Organizational units (`Department`)

Reusable entity (lookup + hierarchy UI). Fields include bilingual names/descriptions, `Code`, `UnitType`, optional `ParentDepartmentId`, `IsActive`, `SortOrder`.

- Parent changes reject self-parent and ancestor/descendant cycles.
- Dedicated **Move** API changes parent only and audits `DepartmentMoved`.
- Soft-deactivate only — blocked when active children or active memberships exist.
- **Reactivate** restores an inactive unit.
- Soft-deactivate only — do not hard-delete departments with history.

### Department membership (`DepartmentMembership`)

- One user → many departments; one department → many users.
- Unique active membership per `(DepartmentId, UserId)` (`EffectiveTo IS NULL`).
- At most one **primary** department per user (`isPrimary` on add / set-primary).
- Batch add members supported for multi-select UI.
- Removing membership is blocked while the user still holds positions in that unit.
- Membership **does not** grant RBAC permissions.

## Position hierarchy

`Position` models a job post **in a department**, with optional `ParentPositionId` forming a tree. Parent cycles are prevented. Cross-department position parents are not allowed.

Starter IT keys remain (idempotent; names/parents not overwritten if the key exists):

- `IT_DIRECTOR` → `IT_MANAGER` → children (`IT_ADMINISTRATOR`, `SYSTEMS_ADMINISTRATOR`, `NETWORK_ADMINISTRATOR`, `IT_SUPPORT_SPECIALIST`, `IT_SECURITY_GOVERNANCE`)

### Multi occupancy (`PositionAssignment`)

- Many users may occupy the same position; one user may hold many positions.
- `IsPrimary` is display-only per user.
- Assigning a user to a position or department **does not** grant RBAC. Access routing selects **users**, not positions.

## Permissions

| Key | Purpose |
| --- | --- |
| `organization.hierarchy.read` | View company/departments/people/positions, profile summaries |
| `organization.hierarchy.manage` | OU CRUD/move/deactivate/reactivate/membership; position CRUD/assignment; set primary |

## UI

Administration → **QEC Hierarchy** → `/administration/hierarchy`

Tabs: **Company View** (OU tree with unit-type badges, counts, expand/collapse, manage actions) · **Departments** (per-unit position chart + PM scope hints) · **People** (primary/other units & positions; Manage Organization) · **Positions**.

## APIs

Under `/api/v1/organization/…`:

- Departments: list/detail/company-view (includes `unitType`, `memberCount`/`peopleCount`, `positionCount`), create/update (with `unitType`), `POST …/move`, `POST …/deactivate`, `POST …/reactivate`
- Members: list/add (`isPrimary`), batch add, remove (position guard), set primary
- People / profile-summary / positions / hierarchy / assignments / active-user search

## Audit

| Aggregate | Field names |
| --- | --- |
| `OrganizationDepartment` | `DepartmentCreated`, `DepartmentUpdated`, `DepartmentMoved`, `DepartmentDeactivated`, `DepartmentReactivated`, `DepartmentMemberAdded`, `DepartmentMemberRemoved`, `PrimaryDepartmentChanged` |
| `OrganizationPosition` | `PositionCreated`, `PositionUpdated`, `PositionParentChanged`, `PositionDeactivated`, `PositionUserAssigned`, `PositionUserRemoved`, `PrimaryPositionChanged` |
