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
    string? RetirementReason, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion,
    int? DaysToReview, bool ReviewDueSoon, bool ReviewOverdue, int? CurrentVersionNumber,
    Guid? CurrentAttachmentId, Guid? CurrentApprovedByUserId, DateTimeOffset? CurrentApprovedAtUtc,
    DateTimeOffset? CurrentPublishedAtUtc);

public sealed record DocumentListResult(IReadOnlyList<DocumentDto> Items, int TotalCount, int Page, int PageSize, int ReviewOverdueCount, int ReviewDueSoonCount);

public sealed record DocumentVersionDto(
    Guid Id, Guid ManagedDocumentId, int VersionNumber, Guid CreatedByUserId, DateTimeOffset CreatedAtUtc,
    string? ChangeSummary, Guid? AttachmentId, Guid? ApprovedByUserId, DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? PublishedAtUtc, Guid? SupersedesVersionId);

public sealed record PolicyAcknowledgementDto(
    Guid Id, Guid ManagedDocumentId, Guid DocumentVersionId, Guid UserId, DateTimeOffset AcknowledgedAtUtc,
    string? DocumentNumber, string? Title, int? VersionNumber);

public sealed record AcknowledgementSummary(int OutstandingForUser, int TotalOutstandingVersions);

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
        bool requiresAcknowledgement, CancellationToken ct)
    {
        ManagedDocument doc = await LoadAsync(id, ct);
        doc.UpdateMetadata(title, ownerUserId, designatedApproverUserId, classification, effectiveDate, reviewDate, requiresAcknowledgement, clock.UtcNow);
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

    public async Task<PolicyAcknowledgementDto> AcknowledgeAsync(Guid documentId, Guid userId, CancellationToken ct)
    {
        ManagedDocument doc = await db.ManagedDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentId, ct)
            ?? throw new InvalidOperationException("Document not found.");
        if (doc.DocumentType != DocumentType.Policy || !doc.RequiresAcknowledgement)
            throw new InvalidOperationException("Document does not require acknowledgement.");
        if (doc.Status != DocumentStatus.Published || doc.CurrentVersionId is null)
            throw new InvalidOperationException("Only the published current version can be acknowledged.");

        bool exists = await db.PolicyAcknowledgements.AnyAsync(
            x => x.DocumentVersionId == doc.CurrentVersionId && x.UserId == userId, ct);
        if (exists) throw new InvalidOperationException("Already acknowledged for this version.");

        PolicyAcknowledgement ack = PolicyAcknowledgement.Create(documentId, doc.CurrentVersionId.Value, userId, clock.UtcNow);
        db.PolicyAcknowledgements.Add(ack);
        await businessAudit.AppendAsync(DocumentAudit.Field(
            documentId, doc.DocumentNumber, "Acknowledged", null, userId.ToString()), ct);
        await db.SaveChangesAsync(ct);

        DocumentVersion? ver = await db.DocumentVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ack.DocumentVersionId, ct);
        return new PolicyAcknowledgementDto(
            ack.Id, ack.ManagedDocumentId, ack.DocumentVersionId, ack.UserId, ack.AcknowledgedAtUtc,
            doc.DocumentNumber, doc.Title, ver?.VersionNumber);
    }

    public async Task<IReadOnlyList<DocumentDto>> ListOutstandingAcknowledgementsAsync(Guid userId, CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        List<ManagedDocument> policies = await db.ManagedDocuments.AsNoTracking()
            .Where(x => x.DocumentType == DocumentType.Policy
                && x.RequiresAcknowledgement
                && x.Status == DocumentStatus.Published
                && x.CurrentVersionId != null
                && x.Classification == DocumentClassification.Internal)
            .ToListAsync(ct);

        List<Guid> versionIds = policies.Select(x => x.CurrentVersionId!.Value).ToList();
        HashSet<Guid> acknowledged = (await db.PolicyAcknowledgements.AsNoTracking()
            .Where(x => x.UserId == userId && versionIds.Contains(x.DocumentVersionId))
            .Select(x => x.DocumentVersionId)
            .ToListAsync(ct)).ToHashSet();

        Dictionary<Guid, DocumentVersion> versions = await LoadVersionsAsync(versionIds, ct);
        return policies
            .Where(x => !acknowledged.Contains(x.CurrentVersionId!.Value))
            .Select(x => Map(x, versions.GetValueOrDefault(x.CurrentVersionId!.Value), now))
            .ToList();
    }

    public async Task<AcknowledgementSummary> GetAcknowledgementSummaryAsync(Guid userId, CancellationToken ct)
    {
        IReadOnlyList<DocumentDto> outstanding = await ListOutstandingAcknowledgementsAsync(userId, ct);
        int totalVersions = await db.ManagedDocuments.AsNoTracking()
            .CountAsync(x => x.DocumentType == DocumentType.Policy
                && x.RequiresAcknowledgement
                && x.Status == DocumentStatus.Published
                && x.CurrentVersionId != null, ct);
        return new AcknowledgementSummary(outstanding.Count, totalVersions);
    }

    public async Task EnsureCatalogSeedAsync(Guid ownerUserId, CancellationToken ct)
    {
        string[] titles =
        [
            "Information Security",
            "Acceptable Use",
            "Access Control",
            "Password",
            "Change Management",
            "Backup",
            "DR/BCP",
            "Third Party",
        ];

        foreach (string title in titles)
        {
            bool exists = await db.ManagedDocuments.AnyAsync(
                x => x.DocumentType == DocumentType.Policy && x.Title == title, ct);
            if (exists) continue;
            await CreateAsync(
                title, DocumentType.Policy, ownerUserId, DocumentClassification.Internal,
                null, null, clock.UtcNow.AddYears(1), requiresAcknowledgement: true,
                ownerUserId, "Initial catalog seed", ct);
        }
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
            x.RequiresAcknowledgement, x.RetirementReason, x.CreatedAtUtc, x.UpdatedAtUtc,
            Convert.ToBase64String(x.RowVersion), x.DaysToReview(now), x.IsReviewDueSoon(now), x.IsReviewOverdue(now),
            version?.VersionNumber, version?.AttachmentId, version?.ApprovedByUserId, version?.ApprovedAtUtc, version?.PublishedAtUtc);

    private static DocumentVersionDto Map(DocumentVersion x) =>
        new(x.Id, x.ManagedDocumentId, x.VersionNumber, x.CreatedByUserId, x.CreatedAtUtc, x.ChangeSummary,
            x.AttachmentId, x.ApprovedByUserId, x.ApprovedAtUtc, x.PublishedAtUtc, x.SupersedesVersionId);
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
