# Cybersecurity checklist strategy

Related: [ISA-315-AUDIT-PROFILE.md](ISA-315-AUDIT-PROFILE.md) · [FRAMEWORK-MAPPING.md](FRAMEWORK-MAPPING.md) · [../03-modules/COMPLIANCE.md](../03-modules/COMPLIANCE.md)

## Purpose

QEC may use internal or regulator-style **cybersecurity checklists** and **external auditor questionnaires**. These are additional `Framework` records with requirement types such as `Question`.

## Seeded profile

| Field | Value |
| --- | --- |
| Code | `QEC-CYBER-READINESS` |
| Profile type | `CybersecurityReadiness` |
| Version | `2026.1` |
| UI | `/it/compliance/readiness/cyber` |

**Important:** `QEC-CYBER-READINESS` is an **internal** cybersecurity readiness checklist. It is **not** ISO 27001 certification, NIST certification, or regulatory attestation.

## Rules

- Do not fork Internal Controls for each questionnaire
- Map questions to existing controls where valid (one control → many requirements)
- Allow **unmapped** questions (gap) rather than fake mapping
- Completing a checklist produces answers + evidence links
- Roll-up does not auto-complete COBIT/ISO requirements except through shared evidence and mapped controls’ assessments
- Operational links (module routes) ≠ compliant; mapping ≠ effective

## Readiness vs effectiveness

Use the shared Framework Readiness engine (`ReadinessService`). Readiness coverage answers “do we have mapping, assessment, and evidence?” Effectiveness comes from assessment **results** and is displayed separately.

## External auditor questionnaires

Store as a framework or as `AuditEngagement` questions linked to FrameworkRequirement. Prefer mapping to controls to reuse evidence.
