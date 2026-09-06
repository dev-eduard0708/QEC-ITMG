# Framework mapping

Related: [CONTROL-MODEL.md](CONTROL-MODEL.md)

## Framework object model

`Framework` (code, name, publisher) → `FrameworkVersion` (e.g. ISO/IEC 27001:2022) → `FrameworkRequirement` (code, text, parent for hierarchy, type: clause/practice/question).

Mappings: `ControlMapping` (control, requirement, relationship `Primary`/`Supporting`, notes).

Many-to-many. A requirement may map to several controls; a control to many requirements.

## Adding a framework later

Insert data + mappings. No schema change. UI is generic.

## Coverage calculations

| Metric | Definition |
|--------|------------|
| Mapped coverage | Distinct applicable requirements with ≥1 mapping |
| Assessment coverage | Applicable requirements with completed assessment on a mapped control |
| Evidence coverage | Applicable requirements with accepted/current evidence on a mapped control |
| Audit readiness coverage | ReadyForReview / Applicable (mapping + assessment + evidence) |
| Compliant etc. | From assessment **results** only (separate from readiness) |
| Evidence missing | Mapped control missing Accepted evidence in period |
| Evidence expired | ValidTo < as-of date |

Never imply certification. Never label readiness as “ISA 315 Compliance %”.

## Applicability and operational links

- `FrameworkRequirementApplicability` — Applicable (default) or NotApplicable (reason required). N/A excluded from readiness denominators.
- `FrameworkRequirementOperationalLink` — safe relative ITMG routes showing where a process is implemented. Not a ControlMapping.

## Framework profile types

`Generic` | `AuditReadiness` | `CybersecurityReadiness` | `Governance` — calculation works for any framework; special UI routes resolve known codes (`ISA315-IT-READINESS`, `QEC-CYBER-READINESS`).

## Frameworks are not equivalent

See strategy docs. Mapping is many-to-many **semantic**, not identity.

## COSO

COSO Internal Control — Integrated Framework addresses **organization-wide internal control** (control environment, risk assessment, control activities, information and communication, monitoring), often associated with financial reporting and enterprise control. It is **not** an IT governance framework (COBIT), **not** an ISMS (ISO/IEC 27001), and **not** a technical safeguard catalog (CIS).

In QEC ITMG, COSO appears as another `Framework` / `FrameworkVersion` with principles/points of focus as requirements. IT-related internal controls may map to COSO principles **where valid**; many COSO principles will map to business/finance controls that ITMG may only **reference**, not operate.

Do not score “COSO compliant” from cybersecurity checklist completion.
