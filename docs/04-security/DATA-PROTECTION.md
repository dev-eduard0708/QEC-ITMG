# Data protection

Related: [../06-data/DATA-CLASSIFICATION.md](../06-data/DATA-CLASSIFICATION.md) · [../01-architecture/FILE-STORAGE-ARCHITECTURE.md](../01-architecture/FILE-STORAGE-ARCHITECTURE.md)

## Classification

Public, Internal, Confidential, Restricted. Default Internal. Security incidents and evidence often Confidential/Restricted.

## Encryption

- TLS 1.2+ (1.3 preferred) on proxy
- SQL TDE or volume encryption
- File volume encryption
- Secrets encrypted; attachment optional per-file encryption if volume encryption insufficient

## Privacy

Ticket bodies may contain personal data of students/staff. Minimize; retention; access logs for Restricted.

## Export

Evidence export and report CSV are classified as the highest item included. Audit actor, reason, timestamp.

## Backup

Encrypted backups, access-controlled restore. Test restores.

## Logs

No secrets; hashed/truncated identifiers where needed.
