# Document management

Related: [POLICY-MANAGEMENT.md](POLICY-MANAGEMENT.md)

## Purpose

Controlled documents: policies, procedures, standards, guidelines, templates, diagrams. Version control, approvals, review dates.

## Model

`ManagedDocument` + `DocumentVersion` (immutable blobs via attachments). Status on the current version head.

## Types

Lookup data, not subclasses in code except where policy acknowledgements need extra behavior.

## Permissions

`doc.read`, `doc.manage`, `doc.approve`

Classification filters apply. Published Internal docs may be employee-visible; Confidential not.

Policies UX is a specialization; do not store policies twice.
