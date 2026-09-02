# Performance testing

- Ticket list 25/50 rows p95 budget (LAN): define in Phase 4 (e.g. 300ms API)
- Search by business number indexed
- Event ingest later: batch and dedup
- SignalR: not for bulk data
- No N+1: integration tests with logging

Scale target v1: hundreds of users, not internet-scale.
