# Evidence strategy

Related: [../03-modules/EVIDENCE-LIBRARY.md](../03-modules/EVIDENCE-LIBRARY.md)

## Reuse

One evidence, many links. Do not duplicate files per framework.

## Quality

EvidenceRequirement on control describes expected type and frequency. Assessment can fail if evidence expired even if file exists.

## Capture paths

1. Manual upload
2. Promote from Change / RestoreTest / AccessReview / Ticket (later automation)
3. Generated reports (CSV of review) attached as evidence

## Validity

`ValidFrom`/`ValidTo` required for periodic controls. Point-in-time evidence (pentest report) has a defined period.

## History

New version supersedes; old remains for the period it covered.
