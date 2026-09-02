# API versioning

- URL prefix `/api/v1`
- Breaking change → `/api/v2` with overlap period
- Additive fields are non-breaking
- SignalR hubs versioned `/hubs/v1/...`

Sunset headers later if needed. Internal-only API still versioned so mobile/AI later does not surprise.
