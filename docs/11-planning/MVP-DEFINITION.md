# MVP definition

Related: [../00-product/SUCCESS-CRITERIA.md](../00-product/SUCCESS-CRITERIA.md) · [IMPLEMENTATION-PHASES.md](IMPLEMENTATION-PHASES.md)

## Recommendation

Ship an MVP that **IT can actually use daily**, not a GRC brochure. Governance modules without tickets and CMDB would be empty shells.

## In MVP

- SSO (or documented staging mock) + users/roles/permissions + org (dept/location)
- Audit history + security audit log
- Numbering, attachments (scan states), comments/timeline, notifications (in-app + email if SMTP)
- Scoped workflow (ticket + change + remote consent)
- Assets + CIs (basic types), assignment, ticket–CI link
- Service desk: SR + incidents, queues, assignment, SLA clocks, KB published read
- Basic change (normal + standard catalog optional; emergency if time)
- Remote: attended request/consent/session record + MeshCentral adapter or mock with same API
- Basic IT dashboard (open tickets, SLA at risk, my work)
- Employee + IT workspaces

## Explicitly not MVP

- Full problem practice (optional link only)
- Broad unattended remote (permission may exist but flag off)
- JML automation, control library, frameworks, evidence module, audit module
- Vuln ingest, BCM, vendors, AI, Teams
- Executive compliance score

## Why include change and remote in MVP

Without change, production work stays in chat. Without remote governance, the most dangerous IT action is off-platform. Both depend on P1–P3.

## Why exclude GRC from MVP

Controls need operational evidence sources first. Mapping empty controls trains bad data.

## MVP phase mapping

Complete P0, P1, P2, P3, P4, P6, P7. P5 incident specialization as part of P4 tickets. P18 only operational widgets. Then production MVP. GRC starts P10–P14 as Release 2.
