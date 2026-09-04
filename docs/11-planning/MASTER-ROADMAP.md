# Master roadmap

Related: [IMPLEMENTATION-PHASES.md](IMPLEMENTATION-PHASES.md) · [MVP-DEFINITION.md](MVP-DEFINITION.md) · [DEPENDENCY-MAP.md](DEPENDENCY-MAP.md)

## Priority path (near term)

After Phase 4 (service desk), prefer:

```
P5 Incidents + problems
 → P6 Change
 → P8 Events / IT ops
 → P9 Access / JML
 → P11–P14 Governance → framework mapping → evidence → audit
```

**Remote support (P7)** remains in product scope but is **lower priority** than the path above (retain attended governance design; schedule after core ITSM/ops/GRC spine unless a hard operational need appears).

**AI (P20) is last.**

## Full sequence (architecture-driven)

```
P0 Foundation
 → P1 Identity, org, RBAC, audit
 → P2 Platform (numbers, files, comments, workflow, notifications)
 → P3 CMDB / assets (ITMG CI + Asset correlation; external AM authoritative for physical lifecycle)
 → P4 Service desk (tickets, SR, SLA)
 → P5 Incidents extras + problems
 → P6 Change
 → P7 Remote support (retained; lower near-term priority)
 → P8 Events / IT ops
 → P9 Access / JML
 → P10 Policy / documents
 → P11 Governance + control library (COBIT governance/control mapping)
 → P12 Framework mapping
 → P13 Evidence library
 → P14 Audit / findings / CAPA (incl. ISA 315–oriented IT audit profile)
 → P15 Security mgmt / vuln / risk
 → P16 BCM / DR / BIA
 → P17 Vendors
 → P18 Advanced reporting
 → P19 Integrations / automation
 → P20 AI assistance
```

**MVP** = P0–P4 + P6 + thin P5 + basic reporting; attended remote when prioritized. See [MVP-DEFINITION.md](MVP-DEFINITION.md).

## AI (P20) examples (future)

Classification, KB suggest, summaries, change risk hints, missing rollback detection, questionnaire mapping, evidence search, NL query. **Must call APIs as the user; never bypass RBAC.**
