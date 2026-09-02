# Audit management

Related: [EVIDENCE-LIBRARY.md](EVIDENCE-LIBRARY.md) · [COMPLIANCE.md](COMPLIANCE.md) · [../05-compliance/AUDIT-READINESS.md](../05-compliance/AUDIT-READINESS.md)

## Purpose

Internal and external audit engagements: requests, questionnaires, findings, observations, CAPA, management responses, evidence requests, export packs.

## Model

- `AuditEngagement` (`AUD-YYYY-NNNNNN`)
- `AuditQuestion` / `AuditRequirement` (may map to FrameworkRequirement)
- `Finding`, `Observation` (observation = finding with severity Informational — or separate type flag)
- `CorrectiveAction`
- `ManagementResponse`
- `EvidenceRequest` (points at Evidence or requests new)

## Export

Generates a package (zip of authorized attachments + manifest). Fully audited. Respect classification; Restricted files need extra permission.

## Permissions

`audit.read`, `audit.manage`, `finding.manage`, `evidence.export`

Auditors get read-only roles without `control.manage`.
