# Control model

Related: [ADR-0007](../12-decisions/ADR-0007-internal-control-first.md) · [FRAMEWORK-MAPPING.md](FRAMEWORK-MAPPING.md)

## Core idea

QEC operates **Internal Controls**. Frameworks are citations. Evidence and assessments attach to controls (and can be pointed at audits).

```
InternalControl
  ├ ControlOwner (user/role)
  ├ TestProcedure
  ├ EvidenceRequirement (what “good evidence” looks like)
  ├ ControlMapping → FrameworkRequirement → FrameworkVersion → Framework
  ├ ControlAssessment (period, result)
  ├ Evidence (many-to-many)
  ├ Finding / CorrectiveAction
  └ PolicyException
```

## InternalControl fields

Number (`CTRL-IAM-004`), title, objective, description, domain, frequency (e.g. quarterly), automation (`Manual`/`Automated`/`ITMG-native`), status (Draft/Active/Retired), linked CIs or processes (optional).

Example: **CTRL-IAM-004 Privileged Access Review** maps to ISO, COBIT, NIST, CIS, internal checklist, and auditor questions **without duplicating the control**.

## Do not

- Clone the control per framework
- Encode COBIT processes as C# enums
- Mark a control Compliant because a mapping exists
