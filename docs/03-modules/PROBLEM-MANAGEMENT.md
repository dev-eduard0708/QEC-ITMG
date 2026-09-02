# Problem management

Related: [INCIDENT-MANAGEMENT.md](INCIDENT-MANAGEMENT.md) · [CHANGE-MANAGEMENT.md](CHANGE-MANAGEMENT.md)

## Definition

A **problem** is the underlying cause (known or suspected) of one or more incidents.

## Aggregate

`Problem`: number `PRB-YYYY-NNNNNN`, status per [STATUS-MODELS](../02-domain/STATUS-MODELS.md), symptoms, root cause, workaround, known error flag, related CIs, related incidents, linked change, owner.

## Rules

- At least one incident should be linked before KnownError (warning, not hard block for draft)
- Closing a problem does not auto-close incidents
- Permanent fix is typically a Change; problem tracks the cause, change tracks the modification

## MVP vs full

MVP: optional create/link problem from incident (thin). Full practice (known error DB, trend analysis, PIR) is Phase 5 completion / post-MVP polish. See [../11-planning/MVP-DEFINITION.md](../11-planning/MVP-DEFINITION.md).

## Permissions

`problem.read`, `problem.manage`
