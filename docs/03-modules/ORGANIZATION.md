# Organization

Related: [ADMINISTRATION.md](ADMINISTRATION.md) · [ACCESS-MANAGEMENT.md](ACCESS-MANAGEMENT.md) · [../04-security/AUTHORIZATION-RBAC.md](../04-security/AUTHORIZATION-RBAC.md) · [../04-security/AUTHENTICATION.md](../04-security/AUTHENTICATION.md)

## Purpose

Organization holds company-wide structure for QEC: **Departments**, **Department membership**, **Locations**, and the **Position hierarchy** (reporting structure of roles/posts per department).

## Identity ownership split

| Concern | Owner |
| --- | --- |
| Display name, email/UPN, profile avatar URL | **Google** (OIDC claims; synced into Identity `User` on login) |
| Departments, membership, positions, hierarchy | **QEC ITMG** (Organization module) |
| RBAC permissions / roles | **QEC ITMG** (Identity) — independent of org structure |
| Access case routing | **Access Management** — selects users, not positions |

Google Admin Directory API is **not** used. Department/job-title claims from Google are **not** trusted for org placement.

## Departments

Reusable `Department` entity (lookup + hierarchy UI). Fields include bilingual names/descriptions, `Code`, optional `ParentDepartmentId`, `IsActive`, `SortOrder`.

Starter departments (idempotent seed): **IT**, **Finance**, **Human Resources**. Existing IT positions are preserved and not recreated.

Soft-deactivate only — do not hard-delete departments with history.

### Department membership (`DepartmentMembership`)

- One user → many departments; one department → many users.
- Unique active membership per `(DepartmentId, UserId)` (`EffectiveTo IS NULL`).
- At most one **primary** department per user.
- Removing membership from Finance does **not** remove IT membership.
- Membership **does not** grant RBAC permissions.

If membership is removed while the user still holds positions in that department, the API requires explicit resolution (warns with open position count) and does not silently delete assignments.

## Position hierarchy

`Position` models a job post **in a department**, with optional `ParentPositionId` forming a tree. Parent cycles are prevented.

Starter IT keys (idempotent seed; names/parents are not overwritten if the key already exists):

- `IT_DIRECTOR` → `IT_MANAGER` → children:
  - `IT_ADMINISTRATOR`
  - `SYSTEMS_ADMINISTRATOR`
  - `NETWORK_ADMINISTRATOR`
  - `IT_SUPPORT_SPECIALIST`
  - `IT_SECURITY_GOVERNANCE`

Vacant positions remain visible — the hierarchy describes structure, not only current people.

### Multi occupancy (`PositionAssignment`)

- Many users may occupy the same position.
- One user may hold many positions.
- Unique constraint: `(PositionId, UserId)`.
- `IsPrimary` marks at most one primary position **per user** (display only).

### Assigning users (department filter)

Default assign picker lists **department members** of the position’s department. Admins may opt into “search all QEC users”. Selecting a non-member requires an explicit **Add to department and assign** confirmation (`addToDepartment`); membership is never created silently.

### Position ≠ permissions / Access routing

Assigning a user to a position or department **does not** grant Identity/RBAC permissions. Access Management routing selects **actual users**, not positions.

## Permissions

| Key | Purpose |
| --- | --- |
| `organization.hierarchy.read` | View company/departments/people/positions, profile summaries |
| `organization.hierarchy.manage` | Department CRUD/membership; position CRUD/assignment; set primary |

Platform Administrator receives all system permissions, including these.

## UI

Administration → **QEC Hierarchy** → `/administration/hierarchy` (legacy `/it/admin/organization/hierarchy` redirects).

Tabs: **Company View** · **Departments** (per-dept position chart) · **People** · **Positions**.

## APIs

Under `/api/v1/organization/…` — departments (CRUD, company-view, members), people, profile-summary, positions list/CRUD, hierarchy tree, assignments (optional `addToDepartment`), active-user search (`departmentId`, `searchAll`).

## Audit

| Aggregate | Field names |
| --- | --- |
| `OrganizationDepartment` | `DepartmentCreated`, `DepartmentUpdated`, `DepartmentDeactivated`, `DepartmentMemberAdded`, `DepartmentMemberRemoved`, `PrimaryDepartmentChanged` |
| `OrganizationPosition` | `PositionCreated`, `PositionUpdated`, `PositionParentChanged`, `PositionDeactivated`, `PositionUserAssigned`, `PositionUserRemoved`, `PrimaryPositionChanged` |

Routine Google avatar/display-name refresh is **not** audited as governance events.
