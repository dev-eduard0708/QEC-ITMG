# Assets and CMDB

Related: [ADR-0006](../12-decisions/ADR-0006-centralized-cmdb.md) · [../02-domain/DOMAIN-MODEL.md](../02-domain/DOMAIN-MODEL.md)

## Two concepts

**Asset:** purchase, cost (optional), vendor, warranty, custody, assignment to user, lifecycle (ordered, in stock, assigned, repair, disposed), serial, financial notes.

**Configuration Item:** operational identity, CI type, environment (prod/test), criticality, owner, support group, relationships, monitoring, RTO/RPO **on the business service**, change/incident history.

They overlap but are not identical. UI may show a combined “laptop” page that edits both records in one use case.

## CI types (initial data, extensible)

Computers, laptops, servers, VMs, network devices, printers, applications, databases, network links, services, integrations/interfaces, endpoints, facilities (limited).

Licenses: **Asset** (and entitlement), not a default infrastructure CI unless used as a software CI for compliance installs.

## Relationships

Typed, directed: `HostedOn`, `DependsOn`, `ConnectsTo`, `RunsOn`, `BackedUpBy`, `AuthenticatedBy`, `ProvidedBy` (vendor is usually FK on CI/asset, not only an edge).

Prevent cycles on selected types if needed (warning first).

## History

Custody transfers, location changes, assignment, disposal — business audit + `AssetCustodyRecord`.

## Network identities

Hostname, MAC, IP (may be multiple) as value table on CI, not a second inventory.

## Governance registers

Applications register, infrastructure register, interface register, network diagrams: **views and attachments** on CMDB + document module, not duplicate masters.

## Permissions

`asset.read`, `asset.manage`, `cmdb.read`, `cmdb.manage`, `cmdb.relationship.manage`

Discovery integrations later **enrich**; they do not bypass authorization to delete.
