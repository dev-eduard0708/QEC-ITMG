# Assets and CMDB

Related: [ADR-0006](../12-decisions/ADR-0006-centralized-cmdb.md) · [../02-domain/DOMAIN-MODEL.md](../02-domain/DOMAIN-MODEL.md)

## Two concepts

**External Asset Management (authoritative):** QEC’s separate physical asset lifecycle system remains the **system of record** for purchase, custody, warranty, disposal, and financial asset lifecycle. ITMG does **not** replace that product.

**ITMG Asset records:** a **compatibility / correlation layer** inside ITMG. They hold enough identifiers and custody pointers to link operational work (tickets, assignment views, audits) to the external asset identity. Prefer sync/correlation over duplicating the full financial register.

**Configuration Item (CI):** operational identity owned by ITMG — CI type, environment, criticality, owner, support group, relationships, monitoring hooks, service dependency. **ITMG owns operational CI/service relationships** and the CMDB graph used by incidents, changes, DR, and security.

Asset and CI overlap but are not identical. UI may show a combined “laptop” page that edits the ITMG Asset correlation record and/or linked CI in one use case; financial truth stays external unless explicitly imported for display.

## CI types (initial data, extensible)

Computers, laptops, servers, VMs, network devices, printers, applications, databases, network links, services, integrations/interfaces, endpoints, facilities (limited).

Seeded network types: `network-device`, `firewall`, `network-link` (plus laptop/server/application/kiosk).

Licenses: **Asset** (and entitlement), not a default infrastructure CI unless used as a software CI for compliance installs.

## Relationships

Typed, directed: `HostedOn`, `DependsOn`, `ConnectsTo`, `RunsOn`, `BackedUpBy`, `AuthenticatedBy`, `ProvidedBy` (vendor is usually FK on CI/asset, not only an edge).

Prevent cycles on selected types if needed (warning first).

## Manual Topology

**Manual Topology** (`/it/cmdb/network-topology`) is the **approved CMDB network truth**.

- Nodes are existing Configuration Items (network-relevant types, `ConnectsTo` participants, or placed layouts).
- Edges are authoritative CMDB `ConnectsTo` relationships.
- Optional `NetworkLinkDetail` stores ports, media, and mode without inventing a second inventory.
- Saved node positions live in `NetworkTopologyView` / `NetworkTopologyNodeLayout`.
- Administrators place unmapped devices and create confirmed connections explicitly.
- Discovery never auto-creates topology links.

## Network Discovery

**Network Discovery** (`/it/cmdb/network-discovery`) is **observed** network data awaiting review.

- Profiles define an explicit IPv4 CIDR (RFC1918 by default), timeout, and concurrency.
- The ITMG backend scans (ICMP + reverse DNS). The browser does **not** scan the LAN.
- Observations are suggestions only (`Matched` / `PossibleMatch` / `New` / `Changed`).
- Humans must Match Existing, Create CI, Accept Changes, or Ignore before CMDB identities change.
- Discovery **enriches** CMDB; it does **not** bypass CMDB governance or silently overwrite trusted CIs.
- Accepted/created CIs appear in Manual Topology as unmapped devices until an admin connects them.

## History

Custody transfers, location changes, assignment, disposal — business audit + `AssetCustodyRecord`.

## Network identities

Hostname, MAC, IP (may be multiple) as `CiNetworkIdentity` value table on CI, not a second inventory.

## Governance registers

Applications register, infrastructure register, interface register, network diagrams: **views and attachments** on CMDB + document module, not duplicate masters.

## Permissions

`asset.read`, `asset.manage`, `cmdb.read`, `cmdb.manage`, `cmdb.relationship.manage`, `cmdb.discovery.manage`

- View topology / discovery results: `cmdb.read`
- Save layouts / manage CIs: `cmdb.manage`
- Create/edit topology connections: `cmdb.relationship.manage`
- Scan and accept discovery results: `cmdb.discovery.manage`

Discovery integrations later **enrich**; they do not bypass authorization to delete.
