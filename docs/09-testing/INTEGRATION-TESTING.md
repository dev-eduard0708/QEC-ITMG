# Integration testing

- Migrations apply cleanly
- Ticket create + history row in one transaction
- SLA job moves clocks
- File storage fake
- OIDC test doubles
- Module DbContexts
- Notification outbox

Use a real SQL Server in CI when possible (Linux container or LocalDB on Windows agents).
