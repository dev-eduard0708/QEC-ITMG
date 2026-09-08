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
├─ Marketing (MARKETING)
└─ Cybersecurity & IT Governance Committee (CYBER_GOV) — UnitType Committee
```

The exact hierarchy remains administrator-configurable. `Department.UnitType` classifies each unit as Company, Department, Section, Office, Team, Committee, or Other.

**HR Portal baseline:** Known Head Office position titles from the QEC HR Portal are the current business-reference baseline for seeded position catalogs. ITMG remains administrator-editable; seeds create missing keys only and do not overwrite admin-customized names/parents.

For the current phase:

- **Departments** (OU rows) represent Head Office functional / administrative units (and the cross-functional committee).
- **Positions** represent Head Office positions/posts (definitions may have **multiple occupants**).
- **DepartmentMembership** organizes Head Office employees.
- **PositionAssignment** assigns employees to Head Office positions.
- **Google** provides identity / name / email / avatar.
- **ITMG** provides organizational placement.
- Department / Position **never** automatically grant RBAC.
- Access Management routing remains separate (selects users, not positions).
- Broader group scope (QSC / QTA / KMG / schools / projects / sites) is deferred.

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
- Cybersecurity & IT Governance Committee (membership-based targeting only)

For V1, **“All Employees”** inside Security Awareness means all employees within the currently enabled **Head Office** awareness scope — **not** automatically every employee working at every QEC project/site. Future organization expansion will extend awareness targeting. Awareness department pickers continue to use `listDepartments` (all active units). Selecting CYBER_GOV targets only employees with active membership in that unit.

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
- Adds LEGAL, MARKETING, CYBER_GOV (Committee), HR sections, and PM child units when missing.
- Attaches orphan starter departments to **QEC** (not EXEC).
- Seeds HR Portal–aligned position catalogs by stable key (create-if-missing only).
- Does **not** seed user `PositionAssignment` rows.
- Soft-deactivates vacant legacy starter keys via `ReconcileLegacyStarterPositionsAsync` (never deletes; never reassigns users).

### Seeded positions by unit (HR Portal baseline)

Reporting trees are editable; the following are the seeded definitions (not one card per employee).

**Executive Management (EXEC)**

```
EXEC-CEO (CEO / الرئيس التنفيذي)
├── EXEC-EXECUTIVE-VP (Executive Vice President / نائب الرئيس التنفيذي)
├── EXEC-EXECUTIVE-SECRETARY (Executive Secretary / السكرتير التنفيذي)
└── EXEC-OFFICE-MANAGER (Office Manager / مدير المكتب)
```

**Finance (FINANCE)**

```
FIN-GENERAL-MANAGER → FIN-ACCOUNTING-MANAGER → FIN-ACCOUNTING-SUPERVISOR → FIN-ACCOUNTANT
```

**Legal Affairs (LEGAL)**

```
LEGAL-GENERAL-MANAGER → LEGAL-RESEARCHER
```

**Information Technology (IT)**

```
IT-GENERAL-MANAGER
├── IT-SYSTEMS-ADMIN
├── IT-NETWORK-TECHNICIAN
├── IT-COMPUTER-ENGINEER
└── IT-PROGRAMMER
```

**Human Resources**

- HR: `HR-DEPUTY-GENERAL-MANAGER` → `HR-SPECIALIST`
- HR_PAYROLL: `HR-PAYROLL-OPERATIONS-MANAGER`, `HR-PAYROLL-SPECIALIST`, `HR-PAYROLL-SENIOR-SPECIALIST`
- HR_GOVREL: `HR-GOV-REL-MANAGER` → specialist / senior specialist
- HR_PERSONNEL: `HR-PERSONNEL-COORDINATOR`
- HR_ADMIN: cleaner / driver / labor / teaboy; logistic supervisor → logistic specialist

**PMO (PM)**

```
PM-GENERAL-PROJECT-MANAGER → PM-DEPUTY-MANAGER
   ├── PM-FIRST-PROGRAMS-MANAGER
   ├── PM-ACADEMIC-SUPERVISOR
   └── PM-EXECUTIVE-SECRETARY
```

- PM_OFFICE: `PM-OFFICE-ADMINISTRATIVE`
- PM_STAFF: `PM-STAFF-PROJECT-COORDINATOR`, `PM-STAFF-TALENT-ACQUISITION-SPECIALIST`

**Marketing:** no positions seeded (no authoritative HR Portal list). Administrator-configurable only.

### Legacy starter reconciliation

After ensuring HR-aligned keys, vacant starter-only legacy positions (zero `PositionAssignment` rows) are soft-deactivated. Occupied or historically assigned keys are preserved. Keys are never renamed.

Examples of legacy keys subject to vacant soft-deactivate: `EXEC-VP`, `EXEC-VP-SECRETARY`, `EXEC-CEO-SECRETARY`, prior IT starter keys (`IT_DIRECTOR`, `IT_MANAGER`, `SYSTEMS_ADMINISTRATOR`, …), `HR_MANAGER`, and prior PM starter keys (`PM-HEAD`, …). New HR codes are created separately (e.g. `IT-GENERAL-MANAGER` alongside a deactivated vacant `IT_MANAGER`).

### Project Management → HR responsibility boundary

Informational organizational responsibility only (no ATS / onboarding workflow in this module):

Project opportunity → delivery → staffing requirements → candidate identification/selection → **endorse to Human Resources** → HR formal employment / onboarding.

## Cybersecurity & IT Governance Committee

| Field | Value |
| --- | --- |
| Code | `CYBER_GOV` (seed input `CYBER-GOV`; department codes normalize hyphens to underscores) |
| Name EN | Cybersecurity & IT Governance Committee |
| Name AR | لجنة الأمن السيبراني وحوكمة تقنية المعلومات |
| UnitType | **Committee** |
| Parent | QEC company root |

Cross-functional QEC Head Office committee for cybersecurity oversight, IT governance, security awareness, technology risk, control coordination, audit readiness, and follow-up of findings and corrective actions.

**Not** a second IT department, **not** an ISA 315 department, **not** an auditor substitute, **not** an RBAC role, and **not** an HR department. ISA 315 remains an audit-readiness profile ([ISA-315-AUDIT-PROFILE.md](../05-compliance/ISA-315-AUDIT-PROFILE.md)); this committee may coordinate control ownership and readiness work without claiming compliance independence.

Seeded vacant positions (no automatic occupants):

```
CYBER-GOV-CHAIR (Committee Chair / Executive Sponsor)
└── CYBER-GOV-LEAD (Cybersecurity & IT Governance Lead)
    ├── CYBER-GOV-COORDINATOR
    ├── CYBER-GOV-PEOPLE
    ├── CYBER-GOV-FINANCE
    └── CYBER-GOV-TECH-SME
```

Assignment rules:

- Administrators assign any active Head Office employee (search is not limited to existing committee members).
- Assigning a non-member uses **Add to committee and assign** → `DepartmentMembership` with `IsPrimary = false`, then the committee position.
- Primary unit and functional positions are preserved.
- Committee membership / position **does not** grant RBAC (`sec.*`, `audit.*`, `governance.*`, etc.).
- Multiple occupants per position (including Technical SME) are supported.

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

A position is a job/post **definition**, not one row per employee. Multiple employees may occupy the same position; one employee may hold many positions.

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

Vacant positions show **Assign Employee**. For Committee units, assign defaults to search-all users and confirms with **Add to committee and assign**.

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
