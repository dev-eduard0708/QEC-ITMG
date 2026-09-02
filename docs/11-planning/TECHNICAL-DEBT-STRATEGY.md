# Technical debt strategy

Allowed debt:

- Mock IdP in development
- Malware scanner `ScanPending` until engine wired (downloads blocked)
- Single host Hangfire
- SignalR in-memory
- Thin problem records
- Manual JML checklists before AD API

Forbidden debt:

- Skipping resource authorization
- Skipping audit history
- Engine as authorization
- Duplicate CI tables
- Hard-coded frameworks
- Secrets in git
- `UpdatedBy` only

Debt is listed in PRs with an expiry phase.
