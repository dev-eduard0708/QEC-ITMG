# Service desk

Related: [INCIDENT-MANAGEMENT.md](INCIDENT-MANAGEMENT.md) · [PROBLEM-MANAGEMENT.md](PROBLEM-MANAGEMENT.md) · [EVENT-MANAGEMENT.md](EVENT-MANAGEMENT.md) · [../02-domain/STATUS-MODELS.md](../02-domain/STATUS-MODELS.md)

## Purpose

The service desk is the employee and technician workspace for **Tickets**: incidents and service requests. It is not the change module, not the audit module, and not a dumping ground for every task in QEC.

## Ticket model

Single aggregate `Ticket`:

| Field group | Content |
|-------------|---------|
| Identity | Id, number (INC/SR), type |
| Classification | Category, subcategory, service offering |
| Priority | Derived from impact × urgency (overridable with reason) |
| People | Requester, assignee, assignment group, watchers |
| Routing | Queue |
| Body | Title, description, channel (portal, phone, email, event) |
| Links | CIs, related tickets, problem, events, remote sessions, changes |
| SLA | Policy snapshot + clocks |
| Closure | Resolution code, root cause hint, reopen count |
| Satisfaction | Optional CSAT after close |

**Support ticket** in UX = the employee’s ticket. Technicians see type badges.

## Distinction

| Type | Use |
|------|-----|
| Service request | Catalog or free-form expected work (access, hardware, how-to) |
| Incident | Unplanned interruption/degradation |
| Inquiry | Optional third type for questions that are neither; if unused, use SR |

Mis-classification: technicians can convert SR → Incident (history recorded). Conversion does not recycle the number; display both numbers or keep original and add Incident number — **decision: keep original number, change type only if still New/Triaged; otherwise create linked incident and close SR as duplicate**. Prefer **linked records** after work has started to preserve SLA honesty.

## Notes

- Public comments: requester visible
- Internal notes: IT only
- Email-in later; portal first

## Assignment

Queues map to groups. `ticket.assign` required. Auto-assign rules are Phase 4 optional; manual is MVP.

## Escalation

Time or priority based. Escalation event notifies manager; does not silently raise privileges.

## Knowledge base

Articles: draft/review/published. Link to tickets. Employee search limited to published non-internal articles.

## SLA

Policies by type, priority, VIP flag, business service. Clocks: first response, resolution. Pause rules. Breach creates notification and metric. SLA engine is server-side (Hangfire ticks).

## Permissions (examples)

`ticket.read`, `ticket.read.internal`, `ticket.create`, `ticket.assign`, `ticket.resolve`, `ticket.close`, `ticket.csat.read`, `kb.read`, `kb.manage`

Resource-level: employees read own; technicians read queue-permitted; security incidents require `ticket.read.security`.

## Out of module

Change approvals, CMDB editing, control assessments.
