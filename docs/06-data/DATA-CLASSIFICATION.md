# Data classification

Levels: **Public**, **Internal**, **Confidential**, **Restricted**.

| Level | Examples | Handling |
|-------|----------|----------|
| Public | Published non-sensitive KB | Broad read |
| Internal | Typical tickets, assets | Authenticated + permission |
| Confidential | HR-related access cases, most evidence | Need-to-know, download audit |
| Restricted | Security incident details, pentest, unattended logs, secrets metadata | Least privilege, 404 hiding, MFA for export |

Defaults: Ticket Internal; Security incident Confidential; Evidence as labeled; Attachment inherits parent unless higher.

Classification is stored on records and attachments. Reporting must not leak Restricted into Employee dashboards.
