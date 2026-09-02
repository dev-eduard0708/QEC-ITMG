# Release strategy

- `main` always releasable after Phase 0
- Features behind flags until phase acceptance
- Migrations versioned; backup before prod migrate
- Forward-only migrations preferred
- Staging = production-like config
- Rollback: previous app bits + **migration rollback plan** (or restore); never leave schema half-applied

PR: tests + docs if contract changes.
