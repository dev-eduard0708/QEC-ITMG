# Database conventions

## Naming

- Schemas short module codes
- Tables singular PascalCase: `Ticket`, `ConfigurationItem`
- Columns PascalCase: `CreatedAtUtc`, `RowVersion`
- PK `Id`
- FK `ConfigurationItemId`
- No `tbl_` prefixes

## Types

- `uniqueidentifier` PK
- `datetimeoffset` for all timestamps (UTC)
- `nvarchar` Unicode
- `rowversion` concurrency
- Money later `decimal(19,4)` if needed

## Indexes

- Unique business number
- Ticket: queue + status + updated
- CI: type, hostname
- History: aggregate id + time
- Filtered unique: e.g. one Active mapping pair

## FKs

`ON DELETE NO ACTION` for audit/history. Restrict delete of User; disable instead.

## Soft delete

`IsDeleted`, `DeletedAtUtc`, `DeletedByUserId`. Unique indexes filtered `WHERE IsDeleted = 0`.

## Migrations

EF Core migrations per module context. Production apply in release, not `EnsureCreated`.

## Uniqueness

NumberSequence + business number unique. UPN unique among non-deleted users.
