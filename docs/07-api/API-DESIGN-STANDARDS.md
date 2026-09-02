# API design standards

Related: [ERROR-CONTRACT.md](ERROR-CONTRACT.md) · [PAGINATION-FILTERING-SORTING.md](PAGINATION-FILTERING-SORTING.md) · [API-VERSIONING.md](API-VERSIONING.md)

## Style

REST, JSON, `/api/v1/{resource}`, HTTPS only.

Resources are nouns: `/tickets`, `/tickets/{id}`, `/tickets/{id}/comments`.

Actions that are not CRUD: `POST /remote-session-requests/{id}/consent` with body `{ "decision": "Allow" }`.

## Auth

Cookie session (BFF) or bearer for services. `Authorization` policies per endpoint.

## Idempotency

`Idempotency-Key` header for create ticket / start remote / evidence export.

## Concurrency

If-Match / `rowVersion` on PUT. 409 on mismatch.

## OpenAPI

Generated; documented errors; examples without real data.

## Time

All timestamps ISO-8601 UTC (`...Z`). Client converts display tz.

## Commands vs queries

POST for commands with side effects. GET safe/idempotent. No GET that starts remote sessions.
