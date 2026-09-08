# ISA 315–oriented IT audit profile

Related: [AUDIT-MANAGEMENT.md](../03-modules/AUDIT-MANAGEMENT.md) · [CONTROL-MODEL.md](CONTROL-MODEL.md) · [FRAMEWORK-MAPPING.md](FRAMEWORK-MAPPING.md) · [../03-modules/COMPLIANCE.md](../03-modules/COMPLIANCE.md) · [../03-modules/ORGANIZATION.md](../03-modules/ORGANIZATION.md) (Cybersecurity & IT Governance Committee — not an ISA 315 department)

## Purpose

Support **IT-related audit readiness** aligned with an **ISA 315–oriented** understanding of IT: how IT systems and controls affect the entity’s financial reporting risk assessment (understanding the entity and its environment, including IT).

This is an **audit profile / questionnaire and evidence packaging lens**, not a claim that ITMG performs statutory audits or replaces the external auditor.

## Seeded profile

| Field | Value |
| --- | --- |
| Code | `ISA315-IT-READINESS` |
| Profile type | `AuditReadiness` |
| Version | `2026.1` |
| UI | `/it/compliance/readiness/isa-315` |

**Important:** `ISA315-IT-READINESS` contains **QEC-authored** audit-readiness questions. It does **not** reproduce or replace the official ISA 315 auditing standard text.

## Framework readiness dashboard

Readiness is calculated per applicable leaf requirement:

| State | Meaning |
| --- | --- |
| NotApplicable | Marked N/A with required reason (excluded from denominator) |
| Unmapped | No active InternalControl mapping |
| MappedNeedsAssessment | Mapped, no completed assessment in period |
| AssessedNeedsEvidence | Completed assessment, no accepted/current evidence |
| ReadyForReview | Mapping + completed assessment + accepted/current evidence |

**Ready for review does not mean effective or Compliant.** A NonCompliant assessment can still be ReadyForReview when documentation and evidence exist for auditor review.

### Coverage formulas (zero-safe)

- Mapped coverage % = Mapped / Applicable × 100
- Assessment coverage % = Assessed / Applicable × 100
- Evidence coverage % = Evidence available / Applicable × 100
- Audit readiness coverage % = ReadyForReview / Applicable × 100

Assessment **result distribution** (Compliant / Partially / NonCompliant / …) is shown separately and is **not** mixed into readiness %.

## Operational links

`FrameworkRequirementOperationalLink` answers “where in ITMG is this process implemented?” via safe relative routes only (for example `/it/access`). Links are **not** ControlMappings and do not imply compliance.

## What it is not

- Not a substitute for COBIT governance mapping
- Not ISO/IEC 27001 certification
- Not automatic “audit passed” / “ISA 315 Compliance = X%” scoring
