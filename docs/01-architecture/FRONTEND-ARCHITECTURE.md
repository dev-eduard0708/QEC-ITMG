# Frontend architecture

Related: [../08-ux/INFORMATION-ARCHITECTURE.md](../08-ux/INFORMATION-ARCHITECTURE.md) · [ADR-0001](../12-decisions/ADR-0001-react-vs-blazor.md) · [ADR-0002](../12-decisions/ADR-0002-vite-vs-nextjs.md)

## Decision

Internal **React 19 + TypeScript + Vite SPA**. Not Blazor (team/product fit and ecosystem for tables, forms, shadcn). Not Next.js (no SEO/SSR need; on-prem SPA behind auth is simpler).

## Stack usage

| Library | Role |
|---------|------|
| React Router | Routes, nested layouts per experience (employee / IT / governance) |
| TanStack Query | Server state, cache, retries |
| TanStack Table | Data grids (tickets, assets, evidence) |
| React Hook Form + Zod | Forms; share Zod schemas with API types where practical |
| Tailwind + shadcn/ui | Design system primitives |
| `@microsoft/signalr` | Live queues, session status, notification badge |
| Recharts | Dashboards ([ADR-0013](../12-decisions/ADR-0013-recharts.md)) |

## Folder intent (`frontend/web/src`)

```
/app          # providers, router, auth bootstrap
/api          # HTTP client, query keys, DTO types
/auth         # session, permission hooks, route guards
/components   # shared UI only
/features     # one folder per module surface
/i18n
/lib          # dates (UTC→display tz), cn(), download helpers
/realtime     # SignalR hub wrappers
```

## State ownership

- **Server state:** TanStack Query only
- **Session:** Auth provider from `/me` + permissions
- **UI chrome:** sidebar open, table column prefs (localStorage, non-sensitive)
- **No Redux** unless a future case is proven

## API layer

- Base URL from configuration
- Credentials: cookie (preferred for SPA same-site) or bearer — see authentication doc
- 401 → re-auth; 403 → permission UI; 409 → concurrency toast
- Never trust UI hiding as security

## Authorization in UI

`can('ticket.assign')` hides actions. Route `requirePermission`. Governance nav only if any governance permission exists. Employee layout is a **separate route tree**, not the IT app with CSS hiding.

## Realtime

Subscribe to hubs scoped by permission. On reconnect, invalidate queries rather than treating SignalR as source of truth.

## Charts and reports

Charts consume **API report endpoints**, not aggregates computed only in the browser from unbounded lists.

## Accessibility and i18n

See [../08-ux/ACCESSIBILITY.md](../08-ux/ACCESSIBILITY.md). All user-visible strings via i18n keys.
