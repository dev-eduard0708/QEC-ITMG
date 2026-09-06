# Compliance

Related: [../05-compliance/CONTROL-MODEL.md](../05-compliance/CONTROL-MODEL.md) · [../05-compliance/FRAMEWORK-MAPPING.md](../05-compliance/FRAMEWORK-MAPPING.md) · [../05-compliance/ISA-315-AUDIT-PROFILE.md](../05-compliance/ISA-315-AUDIT-PROFILE.md) · [EVIDENCE-LIBRARY.md](EVIDENCE-LIBRARY.md)

## Purpose

Load frameworks as **data**, map **internal controls**, run assessments, calendar, exceptions, and **readiness dashboards**. Does not auto-certify QEC.

## Entities

Framework (+ ProfileType, translations), FrameworkVersion, FrameworkRequirement (+ translations), FrameworkRequirementApplicability, FrameworkRequirementOperationalLink, InternalControl, ControlMapping, ControlOwner (on control), TestProcedure, EvidenceRequirement (template), ControlAssessment, PolicyException, ComplianceCalendarItem.

## Framework readiness dashboards

UI:

- `/it/compliance/readiness` — landing
- `/it/compliance/readiness/isa-315` — ISA 315 IT Audit Readiness
- `/it/compliance/readiness/cyber` — Cybersecurity Readiness
- `/it/compliance/readiness/:frameworkCode/requirements/:id` — requirement traceability

API: `GET /api/v1/compliance/readiness…`, applicability + operational-link management under `compliance.manage`.

Seeded starter profiles (idempotent, all environments): `ISA315-IT-READINESS`, `QEC-CYBER-READINESS`.

## Scoring honesty

APIs return breakdowns:

- Mapped / unmapped requirements
- Assessed / unassessed
- Result distribution
- Evidence missing / expired
- Ready for review / not ready
- N/A

Readiness percentages document **control/assessment/evidence coverage**, not statutory compliance or certification. Default wording: “Ready for Audit Review”, never “ISA 315 Passed”.

## Cybersecurity assessment vs COBIT

Completing an internal cybersecurity checklist **may produce evidence** mapped to COBIT-related controls. It does **not** set all COBIT requirements to Compliant.

## Permissions

`compliance.read`, `compliance.manage` (applicability + operational links), `framework.manage` (admin), `control.manage`, `assessment.perform`
