# ADR-0002: Vite SPA instead of Next.js

Date: 2026-09-02
Status: Accepted

## Context

React apps are commonly built with Next.js (SSR/RSC) or Vite (SPA).

## Decision

Use **Vite** to build a **authenticated SPA** served as static files behind the reverse proxy (or from ASP.NET static files).

## Rationale

- No public SEO; all routes require authentication
- On-premises deployment is simpler without a Node SSR farm
- SignalR and cookie/API same-origin are straightforward
- Next.js RSC/auth story is optimized for Vercel/cloud; extra moving parts for internal IT

## Consequences

- No SSR performance for first paint — acceptable on LAN
- Client-side routing; proxy must fallback to `index.html`

## Alternatives considered

- Next.js static export: possible but little benefit over Vite
- Next.js SSR Node service: another process to patch and host
