# Compliance

Related: [../05-compliance/CONTROL-MODEL.md](../05-compliance/CONTROL-MODEL.md) · [../05-compliance/FRAMEWORK-MAPPING.md](../05-compliance/FRAMEWORK-MAPPING.md) · [EVIDENCE-LIBRARY.md](EVIDENCE-LIBRARY.md)

## Purpose

Load frameworks as **data**, map **internal controls**, run assessments, calendar, exceptions. Does not auto-certify QEC.

## Entities

Framework, FrameworkVersion, FrameworkRequirement, InternalControl, ControlMapping, ControlOwner (on control), TestProcedure, EvidenceRequirement (template), ControlAssessment, PolicyException, ComplianceCalendarItem.

## Scoring honesty

APIs return breakdowns:

- Mapped / unmapped requirements
- Assessed / unassessed
- Result distribution
- Evidence missing / expired
- N/A

A single percentage is allowed **only** with documented methodology stored on the report (weighted or unweighted, which framework version, as-of date). Default dashboards show **counts and states**, not a vanity score.

## Cybersecurity assessment vs COBIT

Completing an internal cybersecurity checklist **may produce evidence** mapped to COBIT-related controls. It does **not** set all COBIT requirements to Compliant.

## Permissions

`framework.manage` (admin), `control.manage`, `assessment.perform`, `compliance.read`
