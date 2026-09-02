# Entity catalog

Logical entities. PK GUID unless noted. Schema hints in [../01-architecture/MODULAR-MONOLITH.md](../01-architecture/MODULAR-MONOLITH.md).

## Identity / org / admin

| Entity | Notes |
|--------|-------|
| User | UPN, directory id, status, timezone, type Employee/Vendor/Service |
| Role | Name, description, isSystem |
| Permission | Key unique |
| RolePermission | |
| UserRole | |
| Department | |
| Location | |
| OrganizationalUnit | ParentId |
| LookupValue | Categories, resolution codes |
| SystemSetting | |
| IntegrationCredential | Metadata; secret ref |
| SlaPolicy | |
| WorkflowDefinition / WorkflowState / WorkflowTransition | |
| WorkflowInstance | Parent type/id |

## Platform

| Entity | Notes |
|--------|-------|
| NumberSequence | Prefix+Year unique |
| Attachment | Hash, scan status |
| Comment | Visibility |
| BusinessAuditRecord | Immutable |
| SecurityAuditEvent | Immutable |

## CMDB

| Entity | Notes |
|--------|-------|
| CiType | |
| ConfigurationItem | Number, type, criticality, owner, vendor, engine node, unattended flag |
| CiRelationship | From, To, type unique |
| CiNetworkIdentity | |
| BusinessService | RTO, RPO |
| BusinessServiceCi | |
| Asset | Number, serial, custody, warranty, purchase, optional CI |
| AssetCustodyRecord | |

## Service desk / change / remote / access

| Entity | Notes |
|--------|-------|
| Ticket | Type INC/SR, priority, queue, SLA snapshot |
| TicketCi | |
| TicketWatcher | |
| SlaClock | |
| KnowledgeArticle | |
| Problem | |
| ProblemIncident | |
| ChangeRequest | Type standard/normal/emergency |
| ChangeCi | |
| ChangeApproval | |
| RemoteSessionRequest | |
| RemoteSession | |
| AccessCase | JML type |
| AccessCaseItem | |
| AccessReview / AccessReviewItem | |
| PrivilegedAccount | |
| ServiceAccountRecord | |
| SodRule | |

## Ops / security / BCM / vendor

| Entity | Notes |
|--------|-------|
| OperationalEvent | |
| BackupJob / BackupRun | |
| RestoreTest | |
| CertificateRecord | |
| PatchDeployment | |
| Vulnerability | |
| Risk | |
| PolicyException | |
| ContinuityPlan | |
| BiaRecord | |
| DrTest | |
| Vendor | |
| Contract | |
| VendorAssessment | |

## GRC

| Entity | Notes |
|--------|-------|
| Framework / FrameworkVersion / FrameworkRequirement | |
| InternalControl | |
| ControlMapping | |
| TestProcedure | |
| EvidenceRequirement | |
| ControlAssessment | |
| Evidence | |
| EvidenceLink | |
| ManagedDocument / DocumentVersion | |
| PolicyAcknowledgement | |
| AuditEngagement | |
| AuditQuestion | |
| Finding | |
| CorrectiveAction | |
| EvidenceRequest | |

## Notifications / reporting

| Entity | Notes |
|--------|-------|
| NotificationTemplate | |
| Notification | |
| DeliveryAttempt | |
| NotificationPreference | |
| ReportSnapshot | Phase 18 |
