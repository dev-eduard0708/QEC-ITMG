# Assets and CMDB

Related: [ADR-0006](../12-decisions/ADR-0006-centralized-cmdb.md) · [../02-domain/DOMAIN-MODEL.md](../02-domain/DOMAIN-MODEL.md)

## Two concepts

**External Asset Management (authoritative):** QEC’s separate physical asset lifecycle system remains the **system of record** for purchase, custody, warranty, disposal, and financial asset lifecycle. ITMG does **not** replace that product.

**ITMG Asset records:** a **compatibility / correlation layer** inside ITMG. They hold enough identifiers and custody pointers to link operational work (tickets, assignment views, audits) to the external asset identity. Prefer sync/correlation over duplicating the full financial register.

**Configuration Item (CI):** operational identity owned by ITMG — CI type, environment, criticality, owner, support group, relationships, monitoring hooks, service dependency. **ITMG owns operational CI/service relationships** and the CMDB graph used by incidents, changes, DR, and security.

Asset and CI overlap but are not identical. UI may show a combined “laptop” page that edits the ITMG Asset correlation record and/or linked CI in one use case; financial truth stays external unless explicitly imported for display.

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
