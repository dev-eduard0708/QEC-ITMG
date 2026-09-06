# Access management

Related: [CHANGE-MANAGEMENT.md](CHANGE-MANAGEMENT.md) · [../04-security/AUTHORIZATION-RBAC.md](../04-security/AUTHORIZATION-RBAC.md)

## Purpose

Joiner / Mover / Leaver (JML), access requests, privileged and service accounts, periodic reviews, segregation of duties, exceptions.

This module **does not** replace Entra ID. It records intent, approvals, execution evidence, and reviews. Execution against AD may be checklist in early phases and automated later.

## Practical QEC workflow (category routing)

1. **Configure** an Access Category (IT Access, Door Access, CCTV, …) with bilingual names.
2. Select **actual Active users** for each stage (not job titles):
   - Who can request
   - Who can approve
   - Who can fulfill
   - Who can verify (fallback)
   - Who can close
3. **Request** a case with Category + Type + Subject + items.
4. **Submit** → routing is **snapshotted** onto the case → status becomes Waiting for approval.
5. **Approve** → Fulfillment (eligible fulfillers only).
6. **Send for verification**.
7. **Employee verifies** (preferred) or **IT fallback verifies** with a required reason.
8. **Closer** closes the case (separate from verifier).

Historical in-progress cases keep their original snapshot if an admin later changes the category.

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
