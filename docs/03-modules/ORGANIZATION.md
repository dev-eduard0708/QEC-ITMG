# Organization

Related: [ADMINISTRATION.md](ADMINISTRATION.md) · [ACCESS-MANAGEMENT.md](ACCESS-MANAGEMENT.md) · [../04-security/AUTHORIZATION-RBAC.md](../04-security/AUTHORIZATION-RBAC.md) · [../04-security/AUTHENTICATION.md](../04-security/AUTHENTICATION.md)

## Purpose

Organization holds company-wide structure for QEC: **Departments**, **Department membership**, **Locations**, and the **Position hierarchy** (reporting structure of roles/posts per department).

This organizational reference data is reusable for future targeting (for example policy or security-awareness assignments by company, department, child department, position, or employee). Those workflows are **not** implemented here.

## Identity ownership split

| Concern | Owner |
| --- | --- |
| Display name, email/UPN, profile avatar URL | **Google** (OIDC claims; synced into Identity `User` on login) |
| Departments, membership, positions, hierarchy | **QEC ITMG** (Organization module) |
| RBAC permissions / roles | **QEC ITMG** (Identity) — independent of org structure |
| Access case routing | **Access Management** — selects users, not positions |

Google Admin Directory API is **not** used. Department/job-title claims from Google are **not** trusted for org placement.

## Starter company structure

`Department.ParentDepartmentId` represents cross-department organizational reporting (Company View tree). `Position.ParentPositionId` remains **department-scoped** and cannot point across departments.

```
QEC
└── Executive (EXEC) — root
    ├── Project Management (PM)
    ├── Human Resources (HR)
    ├── Finance (FINANCE)
    └── Information Technology (IT)
```

Idempotent seed creates missing starter departments and may attach IT / Finance / HR / PM to Executive **only when** their `ParentDepartmentId` is null. Manually configured parents are never overwritten.

### Executive positions

```
EXEC-CEO (Chief Executive Officer / الرئيس التنفيذي)
├── EXEC-CEO-SECRETARY (CEO Executive Secretary / السكرتير التنفيذي للرئيس التنفيذي)
└── EXEC-VP (Vice President / نائب الرئيس)
    └── EXEC-VP-SECRETARY (VP Executive Secretary / السكرتير التنفيذي لنائب الرئيس)
```

CEO and Vice President are managerial. Secretaries are non-managerial. Vacant positions remain visible. No automatic user assignments.

### Project Management positions

```
PM-HEAD (Head of Project Management / مدير إدارة المشاريع)
├── PM-PROJECT-MANAGER (Project Manager / مدير مشروع)
│   ├── PM-PROJECT-COORDINATOR (Project Coordinator / منسق مشروع)
│   └── PM-STAFFING-COORDINATOR (Project Staffing Coordinator / منسق القوى العاملة للمشاريع)
└── PM-DEVELOPMENT-COORDINATOR (Project Development Coordinator / منسق تطوير المشاريع)
```

PM-HEAD and PM-PROJECT-MANAGER are managerial.

### Project Management → HR responsibility boundary

Informational organizational responsibility only (no ATS / onboarding workflow in this module):

Project opportunity → delivery → staffing requirements → candidate identification/selection → **endorse to Human Resources** → HR formal employment / onboarding.

**Project Management** may identify or select personnel needed for projects and endorse candidates to HR. **Human Resources** owns formal hiring, employment, and onboarding. Project Management does not bypass HR.

## Departments

Reusable `Department` entity (lookup + hierarchy UI). Fields include bilingual names/descriptions, `Code`, optional `ParentDepartmentId`, `IsActive`, `SortOrder`.

Parent changes reject self-parent and ancestor/descendant cycles.

Soft-deactivate only — do not hard-delete departments with history.

### Department membership (`DepartmentMembership`)

- One user → many departments; one department → many users.
- Unique active membership per `(DepartmentId, UserId)` (`EffectiveTo IS NULL`).
- At most one **primary** department per user.
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
| `organization.hierarchy.manage` | Department CRUD/membership; position CRUD/assignment; set primary |

## UI

Administration → **QEC Hierarchy** → `/administration/hierarchy`

Tabs: **Company View** (department parent tree) · **Departments** (per-dept position chart + PM scope / Executive child-department hints) · **People** · **Positions**.

## APIs

Under `/api/v1/organization/…` — departments (CRUD, company-view with `parentDepartmentId`, members), people, profile-summary, positions, hierarchy tree, assignments (`addToDepartment`), active-user search.

## Audit

| Aggregate | Field names |
| --- | --- |
| `OrganizationDepartment` | `DepartmentCreated`, `DepartmentUpdated`, `DepartmentDeactivated`, `DepartmentMemberAdded`, `DepartmentMemberRemoved`, `PrimaryDepartmentChanged` |
| `OrganizationPosition` | `PositionCreated`, `PositionUpdated`, `PositionParentChanged`, `PositionDeactivated`, `PositionUserAssigned`, `PositionUserRemoved`, `PrimaryPositionChanged` |
