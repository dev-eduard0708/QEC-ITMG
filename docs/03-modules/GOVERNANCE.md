# Governance

Related: [../05-compliance/CONTROL-MODEL.md](../05-compliance/CONTROL-MODEL.md) · [ASSET-CMDB.md](ASSET-CMDB.md) · [COMPLIANCE.md](COMPLIANCE.md)

## Purpose

Enterprise IT governance **workspace**: organization context, registers as views on CMDB, diagrams, and entry to the **control framework hosting** — not a second CMDB.

## Surfaces

- Organization profile (legal name, timezone default, classification scheme)
- IT organization chart (OrganizationalUnit + people)
- Applications / infrastructure / interface **registers** (CMDB queries)
- Network diagrams (ManagedDocument type Diagram + linked CIs)
- Control library **navigation** (data owned with Compliance module)

## COBIT and others

COBIT is a **major reference** for governance processes. It is not identical to ISO 27001 or NIST CSF. Framework content is loaded as data. See [../05-compliance/COBIT-STRATEGY.md](../05-compliance/COBIT-STRATEGY.md).

## Permissions

`gov.read`, `gov.manage`, `control.read`, `control.manage` (control.manage may live in compliance permission set)

## Non-goals

- Drawing packet-level network maps as a Visio replacement (upload + metadata is enough)
- Hard-coding COBIT IDs in C#
