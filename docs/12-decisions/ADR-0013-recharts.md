# ADR-0013: Recharts for dashboards

Date: 2026-09-02
Status: Accepted

## Context

Need a mature React chart library for operational and later executive dashboards.

## Decision

Use **Recharts** (built on D3).

## Rationale

- Widely used, composable, adequate for bar/line/pie/area needed in ITSM/GRC
- Works with React and Tailwind layouts
- Data still comes from server-side report APIs

## Consequences

- Accessibility: provide tables or text summaries with charts
- Not a BI tool replacement (Power BI may exist outside)

## Alternatives considered

- Chart.js / react-chartjs-2: also fine; Recharts preferred for composition
- Apache ECharts: heavier
- Building SVG by hand: rejected
