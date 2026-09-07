# Organization

Related: [ADMINISTRATION.md](ADMINISTRATION.md) · [ACCESS-MANAGEMENT.md](ACCESS-MANAGEMENT.md) · [../04-security/AUTHORIZATION-RBAC.md](../04-security/AUTHORIZATION-RBAC.md)

## Purpose

Organization holds shared reference data used across ITMG: **Departments**, **Locations**, and the **Position hierarchy** (IT organization chart of roles/posts).

## Departments and locations

Departments and locations are simple active/inactive lookups maintained under Administration → Lookups (`admin.lookups`).

## Position hierarchy

`Position` models a job post in a department, with optional `ParentPositionId` forming a tree (Position → Parent Position).

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
- Unique constraint: `(PositionId, UserId)` — duplicate assignment to the same position is blocked.
- Removing one assignment does not remove the user’s other positions.
- `IsPrimary` marks at most one primary position **per user** (display only). Setting primary clears other primaries for that user.

### Position ≠ permissions

Assigning a user to a position **does not** grant Identity/RBAC permissions. Authorization remains permission-based (`organization.hierarchy.*` only controls viewing/managing the hierarchy itself).

### Position ≠ Access routing

Access Management category routing selects **actual users**, not positions. The position tree is independent of Access case approvers/fulfillers.

## Permissions

| Key | Purpose |
| --- | --- |
| `organization.hierarchy.read` | View hierarchy and assignments |
| `organization.hierarchy.manage` | Create/edit/deactivate positions; assign/remove users; set primary; change parent |

Platform Administrator receives all system permissions, including these.

## UI

Administration → **IT Hierarchy** → `/it/admin/organization/hierarchy`

## APIs

Under `/api/v1/organization/…` — positions list/CRUD, hierarchy tree, assignments, user positions, active-user search for assign dialogs.

## Audit

Business audit field names: `PositionCreated`, `PositionUpdated`, `PositionParentChanged`, `PositionDeactivated`, `PositionUserAssigned`, `PositionUserRemoved`, `PrimaryPositionChanged` (`AuditAggregateType.OrganizationPosition`).
