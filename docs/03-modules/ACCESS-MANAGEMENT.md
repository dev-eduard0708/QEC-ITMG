# Access management

Related: [CHANGE-MANAGEMENT.md](CHANGE-MANAGEMENT.md) · [../04-security/AUTHORIZATION-RBAC.md](../04-security/AUTHORIZATION-RBAC.md)

## Purpose

Joiner / Mover / Leaver (JML), access requests, privileged and service accounts, periodic reviews, segregation of duties, exceptions.

This module **does not** replace Entra ID. It records intent, approvals, execution evidence, and reviews. Execution against AD may be checklist in early phases and automated later.

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

`access.request`, `access.approve`, `access.fulfill`, `access.review`, `access.privileged.manage`, `sod.manage`
