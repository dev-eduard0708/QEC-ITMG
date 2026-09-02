# Policy management

Related: [DOCUMENT-MANAGEMENT.md](DOCUMENT-MANAGEMENT.md) · [COMPLIANCE.md](COMPLIANCE.md)

Policies are **ManagedDocuments** with `DocumentType = Policy`. This module is the policy-specific UX and acknowledgement flows on top of document management.

## Required metadata

Number, title, type, owner, approver, version, effective date, review date, classification, status, superseded version, related controls, related framework requirements (via controls preferred), acknowledgements, attachments/body.

## Status

Draft → In Review → Approved → Published → Superseded / Retired. Historic versions immutable.

## Named policies (initial catalog data, not separate tables)

Information security, acceptable use, access control, password, change, backup, DR/BCP, third-party. Additional policies are records, not code.

## Acknowledgements

Optional per policy. Track user, version, time. Reporting: outstanding acknowledgements.

## Reviews

Hangfire: notify owner N days before `ReviewDate`. Overdue policies appear in reports.

## Permissions

`policy.read`, `policy.manage`, `policy.approve`, `policy.acknowledge`

Do not duplicate storage: see Document management for versioning internals.
