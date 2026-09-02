# Error contract

```json
{
  "error": {
    "code": "ticket.notFound",
    "message": "Ticket was not found.",
    "correlationId": "00-...",
    "details": [ { "field": "title", "code": "required", "message": "..." } ]
  }
}
```

| HTTP | When |
|------|------|
| 400 | Validation |
| 401 | Unauthenticated |
| 403 | Permission (unless hiding) |
| 404 | Not found or Restricted hide |
| 409 | Concurrency or invariant conflict |
| 422 | Domain invariant (optional; 409 acceptable if documented) |
| 429 | Rate limit |
| 500 | Unexpected (generic message) |

`code` is stable for clients. `message` is i18n-ready (server may send English; client maps code).
