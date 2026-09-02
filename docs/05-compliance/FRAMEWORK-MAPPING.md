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
| Mapped coverage | Distinct requirements with ≥1 active mapping |
| Assessed coverage | Mapped controls with assessment in period |
| Compliant etc. | From assessment results |
| Evidence missing | Active control missing Accepted evidence in period |
| Evidence expired | ValidTo < as-of date |

Never imply certification.

## Frameworks are not equivalent

See strategy docs. Mapping is many-to-many **semantic**, not identity.

## COSO

COSO Internal Control — Integrated Framework addresses **organization-wide internal control** (control environment, risk assessment, control activities, information and communication, monitoring), often associated with financial reporting and enterprise control. It is **not** an IT governance framework (COBIT), **not** an ISMS (ISO/IEC 27001), and **not** a technical safeguard catalog (CIS).

In QEC ITMG, COSO appears as another `Framework` / `FrameworkVersion` with principles/points of focus as requirements. IT-related internal controls may map to COSO principles **where valid**; many COSO principles will map to business/finance controls that ITMG may only **reference**, not operate.

Do not score “COSO compliant” from cybersecurity checklist completion.
