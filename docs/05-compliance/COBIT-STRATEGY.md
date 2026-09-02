# COBIT strategy

Related: [CONTROL-MODEL.md](CONTROL-MODEL.md) · [../03-modules/GOVERNANCE.md](../03-modules/GOVERNANCE.md)

## Purpose of COBIT (in QEC ITMG)

COBIT is used as a **broad IT governance and management** reference (objectives, practices, organizational enablers). It helps structure governance conversations: evaluate/direct/monitor vs run.

## What COBIT is not

- Not an information security standard like ISO/IEC 27001
- Not a technical control catalog like CIS Controls
- Not COSO (COSO is broader internal control / financial reporting oriented)
- Implementing mapped controls ≠ “COBIT certified” (ISACA certification is a people/program concept anyway)

## How it lives in the platform

Load COBIT **version** as `Framework` data (objectives/practices as requirements). Map QEC Internal Controls. Assessments test **QEC controls**, then roll up to COBIT requirement coverage.

A cybersecurity checklist that maps to some DSS/MEA-related practices does **not** complete EDM governance objectives automatically.

## Product implication

Governance module navigation may group controls by COBIT objective **as a view**, while the source remains InternalControl.
