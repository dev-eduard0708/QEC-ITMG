# File storage architecture

Related: [ADR-0009](../12-decisions/ADR-0009-file-storage.md) · [../04-security/DATA-PROTECTION.md](../04-security/DATA-PROTECTION.md) · [../06-data/DATA-CLASSIFICATION.md](../06-data/DATA-CLASSIFICATION.md)

## Decision

**Central attachment service.** Modules never store unstructured `varbinary` for user files and never write ad-hoc disk paths.

```
Attachment (SQL metadata) → IFileStorage.Save/Get
                              ├─ LocalDiskFileStorage (default on-prem)
                              ├─ SmbFileStorage (later)
                              └─ S3CompatibleFileStorage (later)
```

## Metadata (logical)

See entity `Attachment` in [../06-data/ENTITY-CATALOG.md](../06-data/ENTITY-CATALOG.md):

- Id, hash (SHA-256), size, content type, original name, storage key, classification, owner type/id, uploaded by/at, version group, retention, malware scan status, encryption flag

## Storage key

Opaque, non-guessable key (GUID path). Never use original filename as path. Never serve files via static `/uploads/user-supplied-name`.

## Download

Authenticated API `GET /api/v1/attachments/{id}` with:

- Permission on parent record
- Classification check
- Audit of download for Confidential/Restricted
- Content-Disposition; do not execute in-browser for high-risk types (force download)

## Upload

- Size limits by classification and type
- Allowed MIME/extension allowlist per owner type
- Magic-byte sniff vs claimed type
- Malware scan **architecture**: enqueue Hangfire scan via ICAP/Windows Defender/other engine; file is `Quarantined` until `Clean`. Reject `Infected`. MVP may mark `ScanPending` and block download until scanner is wired — do not skip the state machine.

## Versioning

Policies and evidence may version. Attachments share `VersionGroupId`. Previous blobs are retained per retention policy.

## Integrity

Store hash at upload; verify on download if policy requires. Optional periodic job.

## Retention

Follow [../06-data/RETENTION-ARCHIVING.md](../06-data/RETENTION-ARCHIVING.md). Deleting metadata without blob garbage collection is insufficient; deleting blobs while metadata remains is forbidden.
