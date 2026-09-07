# QEC Organization Expansion Plan

**STATUS:** FUTURE / DEFERRED  
**CURRENT SCOPE:** HEAD OFFICE  
**IMPLEMENTATION:** NOT YET

Related: [../03-modules/ORGANIZATION.md](../03-modules/ORGANIZATION.md) · [MASTER-ROADMAP.md](MASTER-ROADMAP.md)

This document preserves the intended long-term architecture so future work does **not** incorrectly model every project, school, academy, branch, or site as a normal Head Office `Department`.

Nothing in this plan is implemented. Do not treat examples below as seeded data, APIs, or enums.

---

## Current vs future

| | Current | Future (deferred) |
| --- | --- | --- |
| Scope | QEC Head Office | Head Office + projects / schools / branches / academies / sites / field employees |
| Functional structure | `Department` + `Position` | Same Head Office model retained |
| Operational placement | Not modeled | Lightweight operational units (conceptual) |
| Security Awareness | Planned V1 = Head Office only | Broader targeting dimensions |

---

## Conceptual long-term model

```
QEC
│
├─ Head Office
│  ├─ Executive
│  ├─ Project Management
│  ├─ Human Resources
│  ├─ Finance
│  └─ Information Technology
│
└─ Projects / Operations
   ├─ Project / School / Site A
   │  ├─ Management
   │  ├─ Teachers / Trainers
   │  ├─ Administration
   │  └─ Support Staff
   │
   ├─ Project / Branch B
   │  ├─ Branch Manager
   │  └─ Operational Staff
   │
   └─ Future projects/sites
```

This diagram is **conceptual only**. Do not seed or implement these units now.

---

## Important modeling decision

**Do not** model every future QEC project, school, academy, site, or branch as a normal functional `Department`.

Departments are primarily **functional** organizational structures such as:

- Executive
- Project Management
- Human Resources
- Finance
- Information Technology (IT)

A future expansion should introduce or reuse an appropriate lightweight **operational** structure, conceptually something like:

- `OrganizationalUnit`, or
- `ProjectSite`

Possible unit types (examples only — not a fixed catalog):

- Project
- School
- Academy
- Branch
- Site
- Office

Do **not** lock the future implementation to an exact entity name yet. Evaluate the architecture when expansion begins.

---

## Future employee assignment distinction

| Concept | Question it answers |
| --- | --- |
| Department membership | What **functional** organization does this person belong to? |
| Project / site assignment | **Where** or on which **project** does this person currently work? |

### Example — project / field employee

**Ahmed Mohammed**

- Identity: Google
- Functional relationship: project / field employee as appropriate
- Project / site: School Riyadh Project
- Position: Teacher
- Site / location: Riyadh
- Reporting: School Principal / Project Manager as applicable

### Example — Head Office employee with operational support

**Eduard**

- Primary Department: Information Technology
- Head Office Position: IT Administrator
- Future operational assignments (examples):
  - QEC Head Office
  - Quality Training Academy
  - Project X

Supporting several locations must **not** require making each location a `Department`.

---

## Future employee types / positions (examples only)

Project / site employees may later include roles such as:

- Project Manager
- Branch Manager
- School Manager
- Principal
- Teacher
- Trainer
- Administrative Staff
- Support Staff
- Site Coordinator
- other project-specific positions

These are **examples for future design**. Do not add them to current seed. Do not add database enums now.

---

## Project Management / HR relationship

Preserve this business boundary in any future project-employee modeling:

**Project Management**

- Identifies / follows project opportunities
- Determines project staffing requirements
- Identifies / selects / recommends required personnel
- Endorses selected candidates / personnel to HR

**Human Resources**

- Formal employment processing
- Employee records
- Onboarding administration
- Other HR-controlled processes

Project Management does **not** replace HR.

---

## Future Security Awareness expansion

### Current planned V1 (not implemented in Organization)

Security Awareness → **Head Office only**

For V1, “All Employees” means employees in the enabled Head Office awareness scope — not every project/site worker automatically.

### Future targeting dimensions (deferred)

Security Awareness targeting can later support:

- All QEC Employees
- Head Office Employees
- Project / Field Employees
- Department
- Project
- School
- Academy
- Branch
- Site
- Location
- Position
- Specific Employees

Example campaigns (illustrative only):

| Campaign | Audience |
| --- | --- |
| Basic Cybersecurity Awareness | All QEC Employees |
| Executive BEC / Phishing | Executive |
| Finance Fraud Awareness | Finance |
| Student Data Protection | Teachers / school personnel |
| Project Personnel Data Protection | Project Management |
| Branch Security | Branch Managers |

Do **not** implement these future targeting dimensions now.

---

## Future multi-assignment requirements

Record these requirements for when expansion begins:

- One employee may support multiple projects / sites
- One employee may have a primary operational assignment
- One employee may retain a Head Office Department while supporting sites
- Project / site assignments should have effective dates
- History must be retained
- Location should be reusable rather than duplicated
- Project / site assignment must **not** grant RBAC automatically
- Position / project assignment must **not** automatically grant ITMG permissions
- Google identity remains separate from organizational placement

---

## Future reporting / filter dimensions

Likely dimensions:

Employee → Department → Position → Project / Site → Location

Future dashboards may answer:

- Employees by project
- Employees by site
- Employees by position
- Head Office vs project employees
- Security Awareness completion by project / site
- Policy acknowledgement by project / site
- Access cases by organizational unit

No implementation now.

---

## Guardrail for future development

Before implementing project / site hierarchy:

1. Review this document.
2. Do **not** overload `Department` merely to get a quick UI result.
3. Preserve existing Head Office Department / Position data.
4. Add operational-unit support **additively**.
5. Maintain historical employee assignments.
6. Keep identity, organization, RBAC, and Access routing separate.
7. Ensure Head Office-only installations continue to work.
8. Design migration without destructive restructuring.
