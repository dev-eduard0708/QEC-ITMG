# Security testing

Mandatory automated:

- Permission matrix tests per new endpoint
- IDOR tests for tickets, attachments, evidence, remote requests
- Cookie flags
- Upload reject executable types

Periodic:

- Dependency CVE
- Proxy header review
- Threat model delta

Pen tests are recorded in Security module when that phase exists; until then, documents in repo `docs` only.
