# Security Awareness

Related: [SECURITY-MANAGEMENT.md](SECURITY-MANAGEMENT.md) · [ORGANIZATION.md](ORGANIZATION.md) · [../11-planning/ORGANIZATION-EXPANSION-PLAN.md](../11-planning/ORGANIZATION-EXPANSION-PLAN.md)

## Purpose

Security Awareness is **training and knowledge-check** for QEC Head Office employees — not Policy Management.

| Concern | Module |
|---------|--------|
| Official policy read / acknowledge | Document / Policy Management |
| Learn / quiz / completion evidence | **Security Awareness** |

They may reference each other later, but remain separate domains.

## Head Office–only V1 scope

V1 targets **QEC Head Office employees only**, using the currently modeled Organization hierarchy (departments, positions, memberships).

Eligible employees:

- Active Identity users with `UserType.Employee`
- At least one active `DepartmentMembership` (`EffectiveTo` null)
- Vendors, service accounts, and disabled users are excluded

UI wording prefers **“All Head Office Employees”**. If “All Employees” appears in legacy flows, it means the same Head Office-enabled awareness scope — **not** every worker at every QEC project/site.

Deferred (see ORGANIZATION-EXPANSION-PLAN):

- Schools, branches, academies, sites
- Project field workforce / teacher hierarchy
- `ProjectSite` / `OrganizationalUnit` targeting

## Domain model

Schema: `sec` (extends existing Security module).

| Entity | Role |
|--------|------|
| `AwarenessCampaign` | Draft → Active → Closed → Archived; bilingual titles; quiz settings; `RowVersion` |
| `AwarenessCampaignVersion` | Immutable published snapshot at launch |
| `AwarenessContentBlock` | Text / External Link / Video Link / Document Reference (draft or version) |
| `AwarenessAudienceRule` | AllHeadOffice / Department / Position / SpecificUser |
| `AwarenessCampaignQuestion` + `Option` | SingleChoice / MultipleChoice / TrueFalse |
| `AwarenessCompletion` | Assignment with org **snapshots** (name, UPN, department, positions) |
| `AwarenessAttempt` + `AwarenessQuizAnswer` | Quiz attempt history (server-scored) |
| Legacy `AwarenessModule` / `Question` | Older module-based campaigns still supported |

Campaign numbers: `SA-YYYY-######` via `INumberSequenceService` (`security-awareness` / `SA`).

## Lifecycle

1. **Draft** — editable details, audience, content, quiz, schedule  
2. **Launch** — concurrency via `RowVersion`; create version snapshot; resolve DISTINCT audience; create assignment snapshots; status **Active**; notify assignees  
3. **Active** — content/audience frozen for that version; employees complete training  
4. **Close** — no new employee completion activity for outstanding work in the employee list where closed  
5. **Archive** — historical  

Launch is **idempotent**: already-Active campaigns do not duplicate assignments.

## Audience targeting

Supported rules (union, DISTINCT by `UserId`):

- All Head Office Employees  
- Department(s)  
- Position(s)  
- Specific employees  

At launch, audience is **snapshotted**. Later org changes do not rewrite historical assignment department/position fields.

## Employee workflow

Routes:

- `/employee/awareness`
- `/employee/awareness/:assignmentId`  
  Legacy `/employee/security/awareness` redirects to the new list.

Flow:

1. See Required / Completed lists  
2. Start assignment  
3. Read training blocks (EN required; AR falls back to EN)  
4. If quiz required: start attempt → submit → server scores  
5. If no quiz: complete after acknowledging training  
6. Retry when `AllowRetry` and under `MaxAttempts`

Answer keys (`IsCorrect`, explanations) are **not** sent on employee GET before submit.

## Quiz rules

- Single choice / True-False → exactly one correct option  
- Multiple choice → one or more correct  
- Passing score default 80%  
- Server evaluates; browser score is never trusted  

## Reminders

Hangfire job reuses Notifications + email:

- On launch: assigned  
- 7 days before due (`due_7`)  
- 2 days before due (`due_2`)  
- Overdue (`overdue`)  

Deduped via `AwarenessReminderLog`. Deep-link: `/employee/awareness/{assignmentId}`.

## Permissions

| Permission | Use |
|------------|-----|
| `sec.awareness.read` | View campaigns / configuration |
| `sec.awareness.manage` | Create, edit, launch, close, archive |
| `sec.awareness.report` | Completion report + CSV export |

Employee self-service uses session `UserId` only — no admin permission required for own assignments.

## Audit / evidence

BusinessAudit field names include:

`AwarenessCampaignCreated`, `AwarenessCampaignUpdated`, `AwarenessAudienceChanged`, `AwarenessContentChanged`, `AwarenessQuizChanged`, `AwarenessCampaignLaunched`, `AwarenessCampaignClosed`, `AwarenessCampaignArchived`, `AwarenessAssigned`, `AwarenessStarted`, `AwarenessQuizSubmitted`, `AwarenessCompleted`

Authoritative completion rows are the evidence. Do **not** create thousands of Evidence Library files per employee. CSV export supports evidence packaging.

## EN / AR fallback

- English content is required to launch practical campaigns  
- Arabic is recommended, not blocking  
- Employee UI in Arabic falls back to English when AR fields are empty  
- No automatic translation  

## Admin / employee UX

| Surface | Route |
|---------|--------|
| IT dashboard + campaigns | `/it/security/awareness` |
| Campaign builder | `/it/security/awareness/campaigns/:id` |
| My Awareness | `/employee/awareness` |

Legacy module seed/assign UI remains under Security → Awareness tab and links to the new workspace.

## Starter awareness campaigns

Five bilingual Head Office starter campaigns are bootstrapped as editable **Draft** catalog items (production-safe, idempotent):

| StarterKey | Title (EN) |
|------------|------------|
| `SEC-AWARE-PHISHING` | Phishing & Suspicious Email Awareness |
| `SEC-AWARE-PASSWORD` | Password & Authentication Security |
| `SEC-AWARE-DATA` | Data Protection & Confidentiality |
| `SEC-AWARE-COLLAB` | Safe Internet, Email & Collaboration |
| `SEC-AWARE-REMOTE` | Remote Access & Device Security |

Rules:

- Seeded as **Draft only** — no launch, no audience, no schedule/due date, no assignments, no notifications, no quiz attempts, no fake completions
- `StarterKey` is a stable, non-editable seed identity (filtered unique index); title/content edits do **not** cause re-seed or overwrite
- Administrators review content, choose audience (All Head Office Employees / department / position / specific users), set schedule, preview, then launch through the normal V1 flow
- Launch creates an immutable `AwarenessCampaignVersion` and audience snapshot exactly like any user-created campaign
- Starter content supports awareness/training preparation; it does **not** by itself prove that employees were trained
- Evidence of staff awareness begins only after actual campaign assignment and employee completion (assignment, quiz results, reminders, completion timestamps)

Completed awareness campaigns can later support audit readiness questions about ongoing staff awareness training and knowledge assessment — templates alone do **not** claim audit compliance.

## Future expansion

- Project / site / location audience targeting (deferred)  
- **Phishing simulation** — deferred (no simulated phishing send, click tracking, credential capture, landing pages, or tracking pixels in V1)  
- Richer document-picker UX for Document Reference blocks  

## API (V1)

Admin: `/api/v1/security/awareness/dashboard|campaigns...`  
Employee: `/api/v1/me/awareness...`  

Legacy module APIs under `/api/v1/me/security/awareness` and module-campaign create at `/api/v1/security/awareness/module-campaigns` remain for compatibility.
