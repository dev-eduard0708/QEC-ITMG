# Pagination, filtering, sorting

List endpoints:

`GET /api/v1/tickets?page=1&pageSize=25&sort=-updatedAtUtc&status=InProgress&queueId=`

Response:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 25,
  "totalCount": 0
}
```

- `pageSize` max 100 (reports may use dedicated export)
- Sort allowlist (no raw SQL column from client)
- Filter allowlist
- Default sort `-updatedAtUtc`
- Searching: `q=` on number+title, indexed

Export endpoints are separate, permissioned, audited.
