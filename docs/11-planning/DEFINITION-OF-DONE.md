# Definition of Done

A phase or package is done when:

1. Scope in the phase doc is implemented or explicitly deferred with ticket
2. Backend + frontend + database deliverables exist as specified
3. Permissions on every endpoint; IDOR tests for new resources
4. Business audit history for new aggregates
5. UTC, numbering, attachments via platform
6. Tests: unit for invariants, integration for persistence, authz tests
7. OpenAPI updated
8. UX: employee vs IT nav if user-facing
9. Docs: module doc not contradicted; ADR if decision changed
10. Feature flag if not yet production
11. No secrets committed
12. Accessibility smoke for new employee-facing screens

Platform DoD for MVP: [../00-product/SUCCESS-CRITERIA.md](../00-product/SUCCESS-CRITERIA.md) S1–S10.
