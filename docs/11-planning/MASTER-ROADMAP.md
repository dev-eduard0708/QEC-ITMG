# Master roadmap

Related: [IMPLEMENTATION-PHASES.md](IMPLEMENTATION-PHASES.md) · [MVP-DEFINITION.md](MVP-DEFINITION.md) · [DEPENDENCY-MAP.md](DEPENDENCY-MAP.md)

## Sequence (architecture-driven)

```
P0 Foundation
 → P1 Identity, org, RBAC, audit
 → P2 Platform (numbers, files, comments, workflow, notifications)
 → P3 CMDB / assets
 → P4 Service desk (tickets, SR, SLA)
 → P5 Incidents extras + problems
 → P6 Change
 → P7 Remote support
 → P8 Events / IT ops
 → P9 Access / JML
 → P10 Policy / documents
 → P11 Governance + control library
 → P12 Framework mapping
 → P13 Evidence library
 → P14 Audit / findings / CAPA
 → P15 Security mgmt / vuln / risk
 → P16 BCM / DR / BIA
 → P17 Vendors
 → P18 Advanced reporting
 → P19 Integrations / automation
 → P20 AI assistance
```

**MVP** = P0–P4 + P6 + P7 (attended) + thin P5 (incident as ticket type, optional problem link) + basic reporting in P4/P18-lite. See [MVP-DEFINITION.md](MVP-DEFINITION.md).

## AI (P20) examples (future)

Classification, KB suggest, summaries, change risk hints, missing rollback detection, questionnaire mapping, evidence search, NL query: “firewall changes in Q2 and approvals”, “controls with expired evidence”, “critical systems without DR test this year”. **Must call APIs as the user; never bypass RBAC.**
