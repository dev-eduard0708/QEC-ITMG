# Third-party management

Related: [ASSET-CMDB.md](ASSET-CMDB.md) · [ACCESS-MANAGEMENT.md](ACCESS-MANAGEMENT.md) · [COMPLIANCE.md](COMPLIANCE.md)

## Purpose

Vendors, contracts, SLAs, vendor users/access, reviews, risk and security assessments, expiry.

## Model

- `Vendor`
- `Contract` (dates, SLA refs, owner)
- `VendorAssessment`
- Vendor users: Identity users with `UserType = Vendor` **or** `VendorContact` plus optional portal later — **MVP later; Phase 17**
- Vendor access: AccessCase / privileged records tagged with VendorId
- CI.VendorId / Asset.VendorId

## SLA

Contractual SLA is not the service-desk SLA engine, but may reference the same metric definitions.

## Permissions

`vendor.read`, `vendor.manage`, `contract.manage`, `vendor.assess`
