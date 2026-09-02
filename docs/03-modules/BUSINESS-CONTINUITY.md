# Business continuity

Related: [ASSET-CMDB.md](ASSET-CMDB.md) · [IT-OPERATIONS.md](IT-OPERATIONS.md)

## Purpose

BIA, BCP, IT DR, RTO/RPO, recovery procedures, DR tests, critical systems, SPOFs — all **referencing BusinessService and CIs**.

## Model

- `BusinessService` (CMDB) holds RTO/RPO targets
- `BiaRecord` per service/process
- `ContinuityPlan` (BCP vs IT DR types)
- `RecoveryProcedure` (document + CI links)
- `DrTest` with result, evidence, gaps
- SPOF: CI flag or relationship analysis (`SinglePointOfFailure = true` when no redundancy relationship)

## Rules

No duplicate “critical systems list” disconnected from CMDB.

## Permissions

`bcm.read`, `bcm.manage`, `dr.test.manage`
