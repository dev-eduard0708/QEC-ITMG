# Data model overview

Related: [ENTITY-CATALOG.md](ENTITY-CATALOG.md) · [RELATIONSHIP-CATALOG.md](RELATIONSHIP-CATALOG.md) · [../02-domain/DOMAIN-MODEL.md](../02-domain/DOMAIN-MODEL.md)

Conceptual/logical only. **No EF migrations in this phase.**

## Shared vs module-owned

**Shared (referenced everywhere):** User, Role, Permission, Department, Location, Attachment, BusinessAuditRecord, SecurityAuditEvent, Notification (read), ConfigurationItem, Asset, BusinessService, InternalControl, Evidence (linked).

**Module-owned writes:** Ticket (ServiceDesk), ChangeRequest, RemoteSessionRequest, AccessCase, OperationalEvent, Vulnerability, Risk, Framework*, ControlAssessment, AuditEngagement, Vendor, Contract, ManagedDocument, etc.

## Avoid

- Universal `Objects` table
- Duplicate Application tables
- Polymorphic FK for Ticket→CI

## Justified polymorphic

Attachment, Comment, WorkflowInstance, EvidenceLink, Notification source, Timeline.

`EvidenceLink` columns: `EvidenceId`, `TargetType`, `TargetId` **plus** optional typed FKs when target is Control/Audit for query performance (`InternalControlId` nullable AND type enum — pick **either** enum+id **or** several nullable FKs; **decision: several nullable FKs for common targets (Control, Audit, Requirement, Change, Ticket) plus generic overflow for rare types** to keep indexes useful).
