# DR plan for QEC ITMG (the platform)

This is **disaster recovery of the platform itself**, not the BCM module for QEC services.

- RTO/RPO **targets to be set by QEC IT** (proposal: RTO 24h, RPO 1h logs for prod — confirm)
- Cold spare VM or restore to alternate host
- DNS/proxy cutover
- IdP is external dependency
- Engine restore independent; ITMG sessions may show unknown outcome after DR

Periodic restore test recorded as evidence when Evidence module exists.
