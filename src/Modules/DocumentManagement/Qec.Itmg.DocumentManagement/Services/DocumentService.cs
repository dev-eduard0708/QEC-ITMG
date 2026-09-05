using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.DocumentManagement.Domain;
using Qec.Itmg.DocumentManagement.Persistence;

namespace Qec.Itmg.DocumentManagement.Services;

public sealed record DocumentDto(
    Guid Id, string DocumentNumber, string Title, string DocumentType, Guid OwnerUserId,
    Guid? DesignatedApproverUserId, string Classification, string Status, Guid? CurrentVersionId,
    DateTimeOffset? EffectiveDate, DateTimeOffset? ReviewDate, bool RequiresAcknowledgement,
    bool RequireReAcknowledgement,
    string? RetirementReason, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion,
    int? DaysToReview, bool ReviewDueSoon, bool ReviewOverdue, int? CurrentVersionNumber,
    Guid? CurrentAttachmentId, Guid? CurrentApprovedByUserId, DateTimeOffset? CurrentApprovedAtUtc,
    DateTimeOffset? CurrentPublishedAtUtc, string? CurrentContentText);

public sealed record DocumentListResult(IReadOnlyList<DocumentDto> Items, int TotalCount, int Page, int PageSize, int ReviewOverdueCount, int ReviewDueSoonCount);

public sealed record DocumentVersionDto(
    Guid Id, Guid ManagedDocumentId, int VersionNumber, Guid CreatedByUserId, DateTimeOffset CreatedAtUtc,
    string? ChangeSummary, string? ContentText, Guid? AttachmentId, Guid? ApprovedByUserId, DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? PublishedAtUtc, Guid? SupersedesVersionId);

public sealed record PolicyAcknowledgementDto(
    Guid Id, Guid ManagedDocumentId, Guid DocumentVersionId, Guid UserId, DateTimeOffset AcknowledgedAtUtc,
    string? DocumentNumber, string? Title, int? VersionNumber,
    string? AcknowledgementStatementVersion = null, string? Source = null);

public sealed record AcknowledgementSummary(
    int OutstandingForUser,
    int TotalOutstandingVersions,
    int Required = 0,
    int Acknowledged = 0,
    int Overdue = 0);

internal static class DocumentAudit
{
    public static BusinessAuditEntry Created(Guid id, string number) => new()
    {
        AggregateType = AuditAggregateType.Document,
        AggregateId = id,
        BusinessNumber = number,
        Action = BusinessAuditAction.Created,
        Source = AuditSource.Api,
    };

    public static BusinessAuditEntry Field(
        Guid id, string? number, string field, string? oldValue, string? newValue,
        BusinessAuditAction action = BusinessAuditAction.Updated, string? reason = null) => new()
    {
        AggregateType = AuditAggregateType.Document,
        AggregateId = id,
        BusinessNumber = number,
        Action = action,
        FieldName = field,
        OldValue = oldValue,
        NewValue = newValue,
        Reason = reason,
        Source = AuditSource.Api,
    };
}

public sealed class DocumentService(
    DocumentManagementDbContext db,
    INumberSequenceService numbers,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction)
{
    public const string SequenceKey = "documents";
    public const string Prefix = "DOC";

    public async Task<DocumentListResult> ListAsync(
        int page, int pageSize, string? search, DocumentType? type, DocumentStatus? status,
        bool publishedOnly, bool includeConfidential, bool reviewOverdueOnly, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        DateTimeOffset now = clock.UtcNow;
        IQueryable<ManagedDocument> q = db.ManagedDocuments.AsNoTracking();

        if (publishedOnly) q = q.Where(x => x.Status == DocumentStatus.Published);
        if (!includeConfidential)
            q = q.Where(x => x.Classification == DocumentClassification.Internal);
        if (type is DocumentType t) q = q.Where(x => x.DocumentType == t);
        if (status is DocumentStatus s) q = q.Where(x => x.Status == s);
        if (reviewOverdueOnly)
            q = q.Where(x => x.Status == DocumentStatus.Published && x.ReviewDate != null && x.ReviewDate < now);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(x => x.Title.Contains(term) || x.DocumentNumber.Contains(term));
        }

        int total = await q.CountAsync(ct);
        int overdueCount = await db.ManagedDocuments.AsNoTracking()
            .CountAsync(x => x.Status == DocumentStatus.Published && x.ReviewDate != null && x.ReviewDate < now, ct);
        DateTimeOffset soonCutoff = now.AddDays(30);
        int dueSoonCount = await db.ManagedDocuments.AsNoTracking()
            .CountAsync(x => x.Status == DocumentStatus.Published && x.ReviewDate != null
                && x.ReviewDate >= now && x.ReviewDate <= soonCutoff, ct);

        List<ManagedDocument> items = await q.OrderByDescending(x => x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        Dictionary<Guid, DocumentVersion> versions = await LoadVersionsAsync(
            items.Where(x => x.CurrentVersionId.HasValue).Select(x => x.CurrentVersionId!.Value).ToList(), ct);
        return new(
            items.Select(x => Map(x, x.CurrentVersionId is Guid vid && versions.TryGetValue(vid, out DocumentVersion? v) ? v : null, now)).ToList(),
            total, page, pageSize, overdueCount, dueSoonCount);
    }

    public async Task<DocumentDto?> GetAsync(Guid id, bool includeConfidential, bool allowUnpublished, CancellationToken ct)
    {
        ManagedDocument? item = await db.ManagedDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return null;
        if (!allowUnpublished && item.Status != DocumentStatus.Published) return null;
        if (!includeConfidential && item.Classification != DocumentClassification.Internal) return null;
        DocumentVersion? version = null;
        if (item.CurrentVersionId is Guid vid)
            version = await db.DocumentVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == vid, ct);
        return Map(item, version, clock.UtcNow);
    }

    public async Task<DocumentDto> CreateAsync(
        string title, DocumentType type, Guid ownerUserId, DocumentClassification classification,
        Guid? designatedApproverUserId, DateTimeOffset? effectiveDate, DateTimeOffset? reviewDate,
        bool requiresAcknowledgement, Guid actorUserId, string? changeSummary, CancellationToken ct)
    {
        DocumentDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string number = await numbers.NextAsync(SequenceKey, Prefix, innerCt);
            ManagedDocument doc = ManagedDocument.Create(
                number, title, type, ownerUserId, classification, clock.UtcNow,
                designatedApproverUserId, effectiveDate, reviewDate, requiresAcknowledgement);
            db.ManagedDocuments.Add(doc);
            DocumentVersion version = DocumentVersion.Create(doc.Id, 1, actorUserId, clock.UtcNow, changeSummary);
            db.DocumentVersions.Add(version);
            doc.SetCurrentVersion(version.Id, clock.UtcNow);
            await businessAudit.AppendAsync(DocumentAudit.Created(doc.Id, doc.DocumentNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = Map(doc, version, clock.UtcNow);
        }, ct);
        return created!;
    }

    public async Task<DocumentDto> UpdateMetadataAsync(
        Guid id, string title, Guid ownerUserId, Guid? designatedApproverUserId,
        DocumentClassification classification, DateTimeOffset? effectiveDate, DateTimeOffset? reviewDate,
        bool requiresAcknowledgement, bool requireReAcknowledgement, CancellationToken ct)
    {
        ManagedDocument doc = await LoadAsync(id, ct);
        doc.UpdateMetadata(title, ownerUserId, designatedApproverUserId, classification, effectiveDate, reviewDate, requiresAcknowledgement, requireReAcknowledgement, clock.UtcNow);
        await businessAudit.AppendAsync(DocumentAudit.Field(doc.Id, doc.DocumentNumber, "Title", null, title), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, includeConfidential: true, allowUnpublished: true, ct))!;
    }

    public async Task<DocumentVersionDto> CreateRevisionAsync(Guid id, Guid actorUserId, string? changeSummary, CancellationToken ct)
    {
        ManagedDocument doc = await LoadAsync(id, ct);
        if (doc.Status is DocumentStatus.Retired or DocumentStatus.Superseded)
            throw new InvalidOperationException("Cannot revise a terminal document.");
        int next = await db.DocumentVersions.Where(x => x.ManagedDocumentId == id).MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0;
        Guid? supersedes = doc.CurrentVersionId;
        DocumentVersion version = DocumentVersion.Create(id, next + 1, actorUserId, clock.UtcNow, changeSummary, supersedesVersionId: supersedes);
        db.DocumentVersions.Add(version);
        doc.SetCurrentVersion(version.Id, clock.UtcNow);
        if (doc.Status is DocumentStatus.Published or DocumentStatus.Approved or DocumentStatus.InReview)
            doc.TransitionTo(DocumentStatus.Draft, clock.UtcNow);
        await businessAudit.AppendAsync(DocumentAudit.Field(doc.Id, doc.DocumentNumber, "Version", next.ToString(), version.VersionNumber.ToString()), ct);
        await db.SaveChangesAsync(ct);
        return Map(version);
    }

    public async Task AttachToCurrentVersionAsync(Guid id, Guid attachmentId, CancellationToken ct)
    {
        ManagedDocument doc = await LoadAsync(id, ct);
        if (doc.CurrentVersionId is null) throw new InvalidOperationException("Document has no current version.");
        DocumentVersion version = await db.DocumentVersions.FirstOrDefaultAsync(x => x.Id == doc.CurrentVersionId, ct)
            ?? throw new InvalidOperationException("Current version not found.");
        version.Attach(attachmentId);
        await businessAudit.AppendAsync(DocumentAudit.Field(doc.Id, doc.DocumentNumber, "AttachmentId", null, attachmentId.ToString()), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentVersionDto>> ListVersionsAsync(Guid id, CancellationToken ct)
    {
        List<DocumentVersion> items = await db.DocumentVersions.AsNoTracking()
            .Where(x => x.ManagedDocumentId == id).OrderByDescending(x => x.VersionNumber).ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<DocumentDto> SubmitForReviewAsync(Guid id, CancellationToken ct) =>
        await TransitionAsync(id, DocumentStatus.InReview, ct);

    public async Task<DocumentDto> ApproveAsync(Guid id, Guid actorUserId, CancellationToken ct)
    {
        ManagedDocument doc = await LoadAsync(id, ct);
        if (doc.Status != DocumentStatus.InReview)
            throw new InvalidOperationException("Document is not in review.");
        if (actorUserId == doc.OwnerUserId)
            throw new InvalidOperationException("Document owner cannot approve their own document.");
        if (doc.DesignatedApproverUserId is Guid designated && designated != actorUserId)
            throw new InvalidOperationException("Only the designated approver can approve this document.");
        if (doc.CurrentVersionId is null)
            throw new InvalidOperationException("Document has no current version.");
        DocumentVersion version = await db.DocumentVersions.FirstAsync(x => x.Id == doc.CurrentVersionId, ct);
        version.MarkApproved(actorUserId, clock.UtcNow);
        DocumentStatus from = doc.Status;
        doc.TransitionTo(DocumentStatus.Approved, clock.UtcNow);
        await businessAudit.AppendAsync(DocumentAudit.Field(
            doc.Id, doc.DocumentNumber, "Status", from.ToString(), nameof(DocumentStatus.Approved),
            BusinessAuditAction.StatusChanged), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, true, true, ct))!;
    }

    public async Task<DocumentDto> ReturnToDraftAsync(Guid id, string? reason, CancellationToken ct)
    {
        ManagedDocument doc = await LoadAsync(id, ct);
        DocumentStatus from = doc.Status;
        doc.TransitionTo(DocumentStatus.Draft, clock.UtcNow);
        await businessAudit.AppendAsync(DocumentAudit.Field(
            doc.Id, doc.DocumentNumber, "Status", from.ToString(), nameof(DocumentStatus.Draft),
            BusinessAuditAction.StatusChanged, reason), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, true, true, ct))!;
    }

    public async Task<DocumentDto> PublishAsync(Guid id, CancellationToken ct)
    {
        ManagedDocument doc = await LoadAsync(id, ct);
        if (doc.Status != DocumentStatus.Approved)
            throw new InvalidOperationException("Only approved documents can be published.");
        if (doc.CurrentVersionId is null)
            throw new InvalidOperationException("Document has no current version.");
        DocumentVersion version = await db.DocumentVersions.FirstAsync(x => x.Id == doc.CurrentVersionId, ct);
        if (version.ApprovedAtUtc is null)
            throw new InvalidOperationException("Current version is not approved.");

        version.MarkPublished(clock.UtcNow);
        DocumentStatus from = doc.Status;
        doc.TransitionTo(DocumentStatus.Published, clock.UtcNow);
        doc.SetEffectiveDateIfMissing(clock.UtcNow, clock.UtcNow);

        await businessAudit.AppendAsync(DocumentAudit.Field(
            doc.Id, doc.DocumentNumber, "Status", from.ToString(), nameof(DocumentStatus.Published),
            BusinessAuditAction.StatusChanged), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, true, true, ct))!;
    }

    public async Task<DocumentDto> RetireAsync(Guid id, string reason, CancellationToken ct)
    {
        ManagedDocument doc = await LoadAsync(id, ct);
        DocumentStatus from = doc.Status;
        doc.TransitionTo(DocumentStatus.Retired, clock.UtcNow, reason);
        await businessAudit.AppendAsync(DocumentAudit.Field(
            doc.Id, doc.DocumentNumber, "Status", from.ToString(), nameof(DocumentStatus.Retired),
            BusinessAuditAction.StatusChanged, reason), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, true, true, ct))!;
    }

    public async Task<PolicyAcknowledgementDto> AcknowledgeAsync(Guid documentId, Guid userId, CancellationToken ct) =>
        await AcknowledgeAsync(documentId, userId, acceptedStatement: true, clientIp: null, userAgent: null, ct);

    public async Task<PolicyAcknowledgementDto> AcknowledgeAsync(
        Guid documentId,
        Guid userId,
        bool acceptedStatement,
        string? clientIp,
        string? userAgent,
        CancellationToken ct)
    {
        // Delegated path kept for admin-permission route compatibility; prefer PolicyAcknowledgementService.
        ManagedDocument doc = await db.ManagedDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentId, ct)
            ?? throw new InvalidOperationException("Document not found.");
        if (doc.DocumentType != DocumentType.Policy || !doc.RequiresAcknowledgement)
            throw new InvalidOperationException("Document does not require acknowledgement.");
        if (doc.Status != DocumentStatus.Published || doc.CurrentVersionId is null)
            throw new InvalidOperationException("Only the published current version can be acknowledged.");

        DocumentVersion version = await db.DocumentVersions.AsNoTracking()
            .FirstAsync(x => x.Id == doc.CurrentVersionId, ct);

        PolicyAcknowledgement? existing = await db.PolicyAcknowledgements
            .FirstOrDefaultAsync(x => x.DocumentVersionId == version.Id && x.UserId == userId, ct);
        if (existing is not null)
        {
            return new PolicyAcknowledgementDto(
                existing.Id, existing.ManagedDocumentId, existing.DocumentVersionId, existing.UserId,
                existing.AcknowledgedAtUtc, existing.PolicyNumberSnapshot ?? doc.DocumentNumber,
                existing.PolicyTitleSnapshot ?? doc.Title, existing.VersionNumber,
                existing.AcknowledgementStatementVersion, existing.Source);
        }

        if (!acceptedStatement)
            throw new InvalidOperationException("You must confirm that you have read and understood this policy.");

        bool hasAssignment = await db.PolicyAssignments.AnyAsync(
            x => x.DocumentVersionId == version.Id && (
                x.AssignmentScope == PolicyAssignmentScope.AllEmployees
                || (x.AssignmentScope == PolicyAssignmentScope.SpecificUser && x.UserId == userId)), ct);
        if (!hasAssignment)
            throw new InvalidOperationException("This policy is not assigned to you.");

        PolicyAssignment assignment = await db.PolicyAssignments
            .Where(x => x.DocumentVersionId == version.Id && (
                x.AssignmentScope == PolicyAssignmentScope.AllEmployees
                || (x.AssignmentScope == PolicyAssignmentScope.SpecificUser && x.UserId == userId)))
            .OrderByDescending(x => x.AssignedAtUtc)
            .FirstAsync(ct);

        PolicyAcknowledgement ack = PolicyAcknowledgement.Create(
            documentId, version.Id, userId, clock.UtcNow, doc.DocumentNumber, doc.Title, version.VersionNumber,
            assignment.Id, assignment.AssignedAtUtc, assignment.DueAtUtc, clientIp, userAgent);
        db.PolicyAcknowledgements.Add(ack);
        await businessAudit.AppendAsync(DocumentAudit.Field(
            documentId, doc.DocumentNumber, "PolicyAcknowledged", null, userId.ToString()), ct);
        await db.SaveChangesAsync(ct);

        return new PolicyAcknowledgementDto(
            ack.Id, ack.ManagedDocumentId, ack.DocumentVersionId, ack.UserId, ack.AcknowledgedAtUtc,
            ack.PolicyNumberSnapshot, ack.PolicyTitleSnapshot, ack.VersionNumber,
            ack.AcknowledgementStatementVersion, ack.Source);
    }

    public async Task<IReadOnlyList<DocumentDto>> ListOutstandingAcknowledgementsAsync(Guid userId, CancellationToken ct)
    {
        // Prefer assignment-based outstanding; fall back empty when no assignments exist yet.
        List<PolicyAssignment> assignments = await db.PolicyAssignments.AsNoTracking()
            .Where(x => x.IsRequired && (
                x.AssignmentScope == PolicyAssignmentScope.AllEmployees
                || (x.AssignmentScope == PolicyAssignmentScope.SpecificUser && x.UserId == userId)))
            .ToListAsync(ct);

        if (assignments.Count == 0) return [];

        HashSet<Guid> versionIds = assignments.Select(x => x.DocumentVersionId).ToHashSet();
        List<ManagedDocument> policies = await db.ManagedDocuments.AsNoTracking()
            .Where(x => x.DocumentType == DocumentType.Policy
                && x.RequiresAcknowledgement
                && x.Status == DocumentStatus.Published
                && x.CurrentVersionId != null
                && versionIds.Contains(x.CurrentVersionId.Value))
            .ToListAsync(ct);

        HashSet<Guid> currentVersionIds = policies.Select(x => x.CurrentVersionId!.Value).ToHashSet();
        HashSet<Guid> acknowledged = (await db.PolicyAcknowledgements.AsNoTracking()
            .Where(x => x.UserId == userId && currentVersionIds.Contains(x.DocumentVersionId))
            .Select(x => x.DocumentVersionId)
            .ToListAsync(ct)).ToHashSet();

        Dictionary<Guid, DocumentVersion> versions = await LoadVersionsAsync(currentVersionIds.ToList(), ct);
        DateTimeOffset now = clock.UtcNow;
        return policies
            .Where(x => !acknowledged.Contains(x.CurrentVersionId!.Value))
            .Select(x => Map(x, versions.GetValueOrDefault(x.CurrentVersionId!.Value), now))
            .ToList();
    }

    public async Task<AcknowledgementSummary> GetAcknowledgementSummaryAsync(Guid userId, CancellationToken ct)
    {
        IReadOnlyList<DocumentDto> outstanding = await ListOutstandingAcknowledgementsAsync(userId, ct);
        int overdue = 0;
        DateTimeOffset now = clock.UtcNow;
        foreach (DocumentDto item in outstanding)
        {
            if (item.CurrentVersionId is null) continue;
            DateTimeOffset? due = await db.PolicyAssignments.AsNoTracking()
                .Where(x => x.DocumentVersionId == item.CurrentVersionId && (
                    x.AssignmentScope == PolicyAssignmentScope.AllEmployees
                    || (x.AssignmentScope == PolicyAssignmentScope.SpecificUser && x.UserId == userId)))
                .Select(x => x.DueAtUtc)
                .FirstOrDefaultAsync(ct);
            if (due is DateTimeOffset d && d < now) overdue++;
        }

        int required = outstanding.Count;
        // Count acknowledged required current assignments
        List<PolicyAssignment> assignments = await db.PolicyAssignments.AsNoTracking()
            .Where(x => x.IsRequired && (
                x.AssignmentScope == PolicyAssignmentScope.AllEmployees
                || (x.AssignmentScope == PolicyAssignmentScope.SpecificUser && x.UserId == userId)))
            .ToListAsync(ct);
        HashSet<Guid> assignedCurrent = (await db.ManagedDocuments.AsNoTracking()
            .Where(x => x.Status == DocumentStatus.Published && x.CurrentVersionId != null)
            .Select(x => x.CurrentVersionId!.Value)
            .ToListAsync(ct)).ToHashSet();
        HashSet<Guid> assignedVersions = assignments.Select(x => x.DocumentVersionId).Where(assignedCurrent.Contains).ToHashSet();
        int acknowledged = await db.PolicyAcknowledgements.AsNoTracking()
            .CountAsync(x => x.UserId == userId && assignedVersions.Contains(x.DocumentVersionId), ct);
        int requiredTotal = assignedVersions.Count;

        return new AcknowledgementSummary(
            outstanding.Count,
            requiredTotal,
            requiredTotal,
            acknowledged,
            overdue);
    }

    public async Task EnsureCatalogSeedAsync(Guid ownerUserId, CancellationToken ct)
    {
        (string Number, string Title, string Body)[] catalog =
        [
            ("POL-INFOSEC-001", "Information Security Policy",
                "Purpose\nProtect QEC information assets.\n\nEmployee responsibilities\n- Use QEC systems only for authorized work.\n- Protect accounts and devices.\n- Report suspected security incidents promptly.\n- Follow classification and handling rules.\n\nNote\nStarter template for QEC management, IT, information security, and HR/Legal review before approval or publication."),
            ("POL-AUP-001", "Acceptable Use Policy",
                "Purpose\nDefine acceptable use of QEC IT resources.\n\nEmployee responsibilities\n- Do not share credentials.\n- Do not install unauthorized software.\n- Avoid accessing unlawful or inappropriate content.\n- Treat company data confidentially.\n\nNote\nStarter template requiring governance review before publication."),
            ("POL-AUTH-001", "Password & Authentication Policy",
                "Purpose\nProtect access to QEC systems through strong authentication.\n\nEmployee responsibilities\n- Use unique, strong passwords or approved authenticators.\n- Never share passwords or MFA codes.\n- Lock screens when away from the desk.\n- Report suspected account compromise immediately.\n\nNote\nStarter template; not automatically approved."),
            ("POL-DATA-001", "Data Protection & Confidentiality Policy",
                "Purpose\nProtect personal and business data handled by QEC employees.\n\nEmployee responsibilities\n- Collect and share data only when needed for work.\n- Store data in approved systems.\n- Do not send confidential data to personal accounts.\n- Report possible data loss or exposure quickly.\n\nNote\nStarter template for management and privacy review."),
            ("POL-COMMS-001", "Email, Internet & Collaboration Policy",
                "Purpose\nSet expectations for email, internet, and collaboration tools.\n\nEmployee responsibilities\n- Use official QEC accounts for work communication.\n- Be careful with links and attachments.\n- Do not auto-forward mail to personal inboxes.\n- Keep professional tone in company channels.\n\nNote\nStarter template requiring review before publication."),
            ("POL-REMOTE-001", "Remote Access & Remote Support Policy",
                "Purpose\nGovern remote access and remote support to QEC devices.\n\nEmployee responsibilities\n- Use only approved remote access methods.\n- Allow remote support only through official ITMG consent.\n- End remote sessions when work is complete.\n- Report unexpected remote access requests.\n\nNote\nStarter template; must be reviewed before assignment."),
            ("POL-INCIDENT-001", "Information Security Incident Reporting Policy",
                "Purpose\nEnsure security incidents are reported quickly and clearly.\n\nEmployee responsibilities\n- Report phishing, malware, lost devices, and suspicious access.\n- Do not investigate malware yourself.\n- Preserve evidence when asked by IT/security.\n- Use official IT help channels.\n\nNote\nStarter template for QEC security and management review."),
            ("POL-CLEARDESK-001", "Clean Desk & Clear Screen Policy",
                "Purpose\nReduce exposure of sensitive information in workspaces.\n\nEmployee responsibilities\n- Lock screens when leaving the workstation.\n- Store printed sensitive documents securely.\n- Do not leave badges or tokens unattended.\n- Clear whiteboards containing sensitive notes.\n\nNote\nStarter template requiring local policy review before publication."),
        ];

        foreach ((string number, string title, string body) in catalog)
        {
            bool exists = await db.ManagedDocuments.AnyAsync(
                x => x.DocumentType == DocumentType.Policy && x.DocumentNumber == number, ct);
            if (exists) continue;

            ManagedDocument doc = ManagedDocument.Create(
                number, title, DocumentType.Policy, ownerUserId, DocumentClassification.Internal, clock.UtcNow,
                requiresAcknowledgement: true, requireReAcknowledgement: true);
            db.ManagedDocuments.Add(doc);
            DocumentVersion version = DocumentVersion.Create(
                doc.Id, 1, ownerUserId, clock.UtcNow,
                changeSummary: "Starter policy template — requires QEC management review before approval/publication.",
                contentText: body);
            db.DocumentVersions.Add(version);
            doc.SetCurrentVersion(version.Id, clock.UtcNow);
            await businessAudit.AppendAsync(DocumentAudit.Created(doc.Id, doc.DocumentNumber), ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<DocumentDto> TransitionAsync(Guid id, DocumentStatus next, CancellationToken ct, string? reason = null)
    {
        ManagedDocument doc = await LoadAsync(id, ct);
        DocumentStatus from = doc.Status;
        doc.TransitionTo(next, clock.UtcNow, reason);
        await businessAudit.AppendAsync(DocumentAudit.Field(
            doc.Id, doc.DocumentNumber, "Status", from.ToString(), next.ToString(),
            BusinessAuditAction.StatusChanged, reason), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, true, true, ct))!;
    }

    private async Task<ManagedDocument> LoadAsync(Guid id, CancellationToken ct) =>
        await db.ManagedDocuments.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new InvalidOperationException("Document not found.");

    private async Task<Dictionary<Guid, DocumentVersion>> LoadVersionsAsync(List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        return await db.DocumentVersions.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
    }

    private static DocumentDto Map(ManagedDocument x, DocumentVersion? version, DateTimeOffset now) =>
        new(x.Id, x.DocumentNumber, x.Title, x.DocumentType.ToString(), x.OwnerUserId, x.DesignatedApproverUserId,
            x.Classification.ToString(), x.Status.ToString(), x.CurrentVersionId, x.EffectiveDate, x.ReviewDate,
            x.RequiresAcknowledgement, x.RequireReAcknowledgement, x.RetirementReason, x.CreatedAtUtc, x.UpdatedAtUtc,
            Convert.ToBase64String(x.RowVersion), x.DaysToReview(now), x.IsReviewDueSoon(now), x.IsReviewOverdue(now),
            version?.VersionNumber, version?.AttachmentId, version?.ApprovedByUserId, version?.ApprovedAtUtc, version?.PublishedAtUtc,
            version?.ContentText);

    private static DocumentVersionDto Map(DocumentVersion x) =>
        new(x.Id, x.ManagedDocumentId, x.VersionNumber, x.CreatedByUserId, x.CreatedAtUtc, x.ChangeSummary,
            x.ContentText, x.AttachmentId, x.ApprovedByUserId, x.ApprovedAtUtc, x.PublishedAtUtc, x.SupersedesVersionId);
}

public sealed record DocumentReviewCandidate(
    Guid DocumentId, string DocumentNumber, string Title, Guid OwnerUserId,
    DateTimeOffset ReviewDateUtc, int DaysToReview, int ThresholdDays);

public sealed class DocumentReviewNotificationService(DocumentManagementDbContext db, IClock clock)
{
    public static readonly int[] Thresholds = [30, 14, 7, 1, 0];

    public async Task<IReadOnlyList<DocumentReviewCandidate>> FindDueNotificationsAsync(CancellationToken ct = default)
    {
        DateTimeOffset now = clock.UtcNow;
        List<ManagedDocument> docs = await db.ManagedDocuments.AsNoTracking()
            .Where(x => x.Status == DocumentStatus.Published && x.ReviewDate != null)
            .ToListAsync(ct);

        HashSet<(Guid, DateTimeOffset, int)> already = (await db.DocumentReviewNotificationLogs.AsNoTracking()
            .Select(x => new { x.ManagedDocumentId, x.ReviewDateUtc, x.ThresholdDays })
            .ToListAsync(ct))
            .Select(x => (x.ManagedDocumentId, x.ReviewDateUtc, x.ThresholdDays))
            .ToHashSet();

        List<DocumentReviewCandidate> due = [];
        foreach (ManagedDocument doc in docs)
        {
            DateTimeOffset review = doc.ReviewDate!.Value;
            int days = doc.DaysToReview(now) ?? 0;
            foreach (int threshold in Thresholds)
            {
                bool crossed = threshold == 0
                    ? doc.IsReviewOverdue(now)
                    : !doc.IsReviewOverdue(now) && days <= threshold;
                if (!crossed || already.Contains((doc.Id, review, threshold))) continue;
                due.Add(new DocumentReviewCandidate(
                    doc.Id, doc.DocumentNumber, doc.Title, doc.OwnerUserId, review, days, threshold));
            }
        }

        return due;
    }

    public async Task MarkNotifiedAsync(Guid documentId, DateTimeOffset reviewDateUtc, int thresholdDays, CancellationToken ct = default)
    {
        bool exists = await db.DocumentReviewNotificationLogs.AnyAsync(
            x => x.ManagedDocumentId == documentId && x.ReviewDateUtc == reviewDateUtc && x.ThresholdDays == thresholdDays, ct);
        if (exists) return;
        db.DocumentReviewNotificationLogs.Add(
            DocumentReviewNotificationLog.Create(documentId, reviewDateUtc, thresholdDays, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }
}
