# Access management

Related: [CHANGE-MANAGEMENT.md](CHANGE-MANAGEMENT.md) · [../04-security/AUTHORIZATION-RBAC.md](../04-security/AUTHORIZATION-RBAC.md)

## Purpose

Joiner / Mover / Leaver (JML), access requests, privileged and service accounts, periodic reviews, segregation of duties, exceptions.

This module **does not** replace Entra ID. It records intent, approvals, execution evidence, and reviews. Execution against AD may be checklist in early phases and automated later.

## Access entitlement catalog

Global reusable `AccessEntitlement` records (bilingual names, `DefaultRevokeAction` Remove|Disable, privileged flag) are mapped to categories via `AccessCategoryEntitlement` with `IsDefaultForJoiner` and sort order.

- **Default for Joiner** means pre-selected on Joiner create — not mandatory; requesters may uncheck.
- One entitlement may belong to multiple categories.
- Case items snapshot entitlement id and friendly names so later catalog edits do not rewrite history.
- Custom case-specific access (`IsCustom`) does not pollute the global catalog.

## Current access register

`UserAccessEntitlement` is ITMG's governed record of what access a subject currently has, updated **only when an AccessCase reaches Closed** after verification (Grant→Active, Remove→Removed, Disable→Disabled). It is **not** authoritative AD/Google/ERP directory state.

## Practical QEC workflow (category routing)

1. **Configure** an Access Category (IT Access, Door Access, CCTV, …) with bilingual names.
2. Select **actual Active users** for each stage (not job titles):
   - Who can request
   - Who can approve
   - Who can fulfill
   - Who can verify (fallback)
   - Who can close
3. **Create** a case with Category + Type + Subject + items:
   - **Save as Draft** (`submitForApproval: false`) → status remains Draft; category name snapshot/display is filled for drafts.
   - **Create & Submit for Approval** (`submitForApproval: true`) → routing is **snapshotted** onto the case → status becomes **Approval** (waiting for approval); approvers are notified.
4. **Approver** (route snapshot + `access.approve`) chooses one of:
   - **Approve** → Fulfillment; fulfillers notified. Empty approver route does **not** open approval to everyone with the permission.
   - **Send for Rework** (reason required) → status **Rework**; requester notified; current scope revision marked Rework. Routing snapshot stays stable.
   - **Reject** (reason required) → terminal **Rejected** (not Closed). Rejection reason is stored on the case.
5. **Rework**: requester may edit **reason + requested items only** (category / type / subject / routing remain locked). **Resubmit** creates the next Pending scope revision and returns to Approval; approvers are notified again.
6. **Fulfiller** completes **exact** requested items and **Send for verification** → Verification; subject/fallback verifiers notified. Fulfillers cannot add/expand scope; late access requires a **new case**.
7. **Verify**: subject employee (preferred) or authenticated fallback verifier (server enforces route; no `access.request` required on verify endpoints).
8. **Closer** (route snapshot + `access.fulfill`) closes when verified / ready-to-close → **Closed** (successful completion only); requester/subject notified; stage notifications resolved.
9. Historical in-progress cases keep their original snapshot if an admin later changes the category.

### Approval decisions & scope revisions

| Decision | Next status | Notes |
| --- | --- | --- |
| Approve | Fulfillment | Marks current revision Approved |
| Send for Rework | Rework | Marks current revision Rework; requester edits then resubmits |
| Reject | Rejected | Terminal; marks current revision Rejected. **Rejected ≠ Closed** |

Each submit/resubmit snapshots items into `AccessCaseRevision` / `AccessCaseRevisionItem`. `CurrentScopeRevisionNumber` tracks the active Pending revision.

### Scope freeze & server capabilities

- **Draft or Rework**: requested items (and rework reason) may be edited (requester / `access.request` / configure as applicable).
- **After submit** (Approval and beyond, except Rework): requested scope is **locked**. Case detail returns an `actions` capability block (`canApprove`, `canSendForRework`, `canEditRequest`, `canResubmit`, …) resolved from session permissions + snapshotted route — the UI must treat it as authoritative.
- Reject and Send for Rework require a non-empty reason.
- **Closed** is only for the successful Verification → Close path.

### Work queues & notifications

Frontend lists poll every 12s (and on window focus). Queues: My requests, For approval, For fulfillment, For verification, For closure (All for `access.configure` only). List/detail routes accept any of `access.request|approve|fulfill|configure|review|privileged.manage|sod.manage`. In-app notifications include user-scoped query keys and refresh on persona/user switch.

## Employee verification vs IT fallback

- Category setting `PreferSubjectEmployeeVerification` prefers the subject employee as verifier for Joiner/Mover/AccessRequest when `SubjectUserId` exists.
- Configured Verifier users are authorized **fallback** verifiers (`Verify on behalf` + reason).
- Leaver cases do **not** require the departed employee to verify.

## Development demo personas

In **Development only**, seven demo users and two starter categories (`IT_ACCESS`, `DOOR_ACCESS`) are seeded idempotently. Quick-login personas appear on the login page.

**Never** seed demo personas in Staging/Production. Guarded by `IHostEnvironment.IsDevelopment()`.

## Joiner

HR/authorized requester → manager approval → application/CI access list → IT fulfillment → verification → evidence (EVD link).

## Mover

Must list **existing** access (from last review or directory snapshot if available) before adding new. Removal tasks generated for old department defaults.

## Leaver

Checklist items (data-configured): AD disable, mailbox, VPN, app access, privileged removal, asset recovery, service-account ownership reassignment. Cannot close until mandatory items done or exception filed.

## Access request (ad-hoc)

Same `AccessCase` type discriminator. May spawn a service request ticket for queue work, or be the work itself — **decision: AccessCase is canonical for JML; service desk SR can be created as a child work order for technicians.**

## Reviews

Campaigns: user access, privileged, service accounts. Reviewer attestations stored as evidence.

## SoD

Rules table: conflicting permission pairs or app role pairs. Violations block or require exception.

## Permissions

`access.request`, `access.approve`, `access.fulfill`, `access.configure`, `access.review`, `access.privileged.manage`, `sod.manage`

Authorization for stage actions requires **both** the global permission (where applicable) **and** case routing eligibility from the snapshot.
