# Dependency map

```
Identity ────────┐
Organization ────┼► Platform (numbers, files, workflow, comments, audit)
                 └► Notifications

Platform ─► CMDB/Assets ─► ServiceDesk ─► Incident/Problem
                │              │
                ├──────────────┼► Change
                │              │
                ├──────────────┴► RemoteSupport
                │
                ├► Access/JML
                ├► IT Operations/Events
                ├► Security (vuln on CI)
                ├► BCM (RTO on service)
                └► Vendors (FK on CI)

Documents/Policy ─► (links) Controls
CMDB + Platform ─► Control library
Controls ─► Framework mapping
Controls + Files ─► Evidence
Evidence + Controls ─► Audit
All ─► Advanced reporting
Integrations ─► Events, vulns, AD JML, engine
AI ─► all APIs + authz
```

Remote support **must not** precede identity, CMDB, and ticket (attended). Change should precede unattended production remote.
