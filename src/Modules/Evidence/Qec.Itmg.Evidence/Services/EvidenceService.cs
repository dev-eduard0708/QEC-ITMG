using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Evidence;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.Evidence.Domain;
using Qec.Itmg.Evidence.Persistence;

namespace Qec.Itmg.Evidence.Services;

public sealed record EvidenceDto(
    Guid Id, string EvidenceNumber, string Title, string? Description, Guid OwnerUserId,
    string SourceType, Guid? SourceRecordId, string EvidenceType, string Classification,
    DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo, DateTimeOffset CapturedAtUtc, string Status,
    Guid? CurrentVersionId, Guid? AcceptedByUserId, DateTimeOffset? AcceptedAtUtc, string? WithdrawalReason,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion,
    int? DaysToExpiry, bool IsExpired, bool IsExpiringSoon, Guid? CurrentAttachmentId, int? CurrentVersionNumber);

public sealed record EvidenceListResult(IReadOnlyList<EvidenceDto> Items, int TotalCount, int Page, int PageSize, int ExpiredCount, int ExpiringSoonCount);

public sealed record EvidenceVersionDto(
    Guid Id, Guid EvidenceId, int VersionNumber, Guid AttachmentId, Guid CreatedByUserId,
    DateTimeOffset CreatedAtUtc, string? ChangeSummary, Guid? SupersedesVersionId);

public sealed record EvidenceLinkDto(
    Guid Id, Guid EvidenceId, string TargetType, Guid TargetId, Guid CreatedByUserId, DateTimeOffset CreatedAtUtc);

internal static class EvidenceAudit
{
    public static BusinessAuditEntry Created(Guid id, string number) => new()
    {
        AggregateType = AuditAggregateType.Evidence,
        AggregateId = id,
        BusinessNumber = number,
        Action = BusinessAuditAction.Created,
        Source = AuditSource.Api,
    };

    public static BusinessAuditEntry Field(
        Guid id, string? number, string field, string? oldValue, string? newValue,
        BusinessAuditAction action = BusinessAuditAction.Updated, string? reason = null) => new()
    {
        AggregateType = AuditAggregateType.Evidence,
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

public sealed class EvidenceService(
    EvidenceDbContext db,
    INumberSequenceService numbers,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction) : IEvidenceCoverageQuery
{
    public const string SequenceKey = "evidence";
    public const string Prefix = "EVD";
    public const string AttachmentResourceType = "Evidence";

    public async Task<EvidenceListResult> ListAsync(
        int page, int pageSize, string? search, EvidenceStatus? status, EvidenceType? type,
        EvidenceSourceType? source, EvidenceClassification? classification, Guid? ownerUserId,
        bool expiredOnly, bool expiringSoonOnly, bool includeConfidential, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        DateTimeOffset now = clock.UtcNow;
        IQueryable<EvidenceRecord> q = db.EvidenceRecords.AsNoTracking();
        if (!includeConfidential)
            q = q.Where(x => x.Classification == EvidenceClassification.Internal);
        if (status is EvidenceStatus s) q = q.Where(x => x.Status == s);
        if (type is EvidenceType t) q = q.Where(x => x.EvidenceType == t);
        if (source is EvidenceSourceType src) q = q.Where(x => x.SourceType == src);
        if (classification is EvidenceClassification c) q = q.Where(x => x.Classification == c);
        if (ownerUserId is Guid oid) q = q.Where(x => x.OwnerUserId == oid);
        if (expiredOnly)
            q = q.Where(x => x.Status == EvidenceStatus.Expired
                || (x.Status == EvidenceStatus.Accepted && x.ValidTo != null && x.ValidTo < now));
        if (expiringSoonOnly)
        {
            DateTimeOffset soon = now.AddDays(30);
            q = q.Where(x => x.Status == EvidenceStatus.Accepted && x.ValidTo != null
                && x.ValidTo >= now && x.ValidTo <= soon);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(x => x.Title.Contains(term) || x.EvidenceNumber.Contains(term));
        }

        int total = await q.CountAsync(ct);
        int expiredCount = await db.EvidenceRecords.AsNoTracking().CountAsync(
            x => x.Status == EvidenceStatus.Expired
                || (x.Status == EvidenceStatus.Accepted && x.ValidTo != null && x.ValidTo < now), ct);
        DateTimeOffset soonCutoff = now.AddDays(30);
        int expiringCount = await db.EvidenceRecords.AsNoTracking().CountAsync(
            x => x.Status == EvidenceStatus.Accepted && x.ValidTo != null
                && x.ValidTo >= now && x.ValidTo <= soonCutoff, ct);

        List<EvidenceRecord> items = await q.OrderByDescending(x => x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        Dictionary<Guid, EvidenceVersion> versions = await LoadVersions(
            items.Where(x => x.CurrentVersionId.HasValue).Select(x => x.CurrentVersionId!.Value).ToList(), ct);
        return new(
            items.Select(x => Map(x, x.CurrentVersionId is Guid vid && versions.TryGetValue(vid, out EvidenceVersion? v) ? v : null, now)).ToList(),
            total, page, pageSize, expiredCount, expiringCount);
    }

    public async Task<EvidenceDto?> GetAsync(Guid id, bool includeConfidential, CancellationToken ct)
    {
        EvidenceRecord? item = await db.EvidenceRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return null;
        if (!includeConfidential && item.Classification != EvidenceClassification.Internal) return null;
        EvidenceVersion? version = null;
        if (item.CurrentVersionId is Guid vid)
            version = await db.EvidenceVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == vid, ct);
        return Map(item, version, clock.UtcNow);
    }

    public async Task<EvidenceDto> CreateAsync(
        string title, Guid ownerUserId, EvidenceSourceType sourceType, EvidenceType evidenceType,
        EvidenceClassification classification, DateTimeOffset? capturedAtUtc, string? description,
        Guid? sourceRecordId, DateTimeOffset? validFrom, DateTimeOffset? validTo,
        Guid? attachmentId, Guid actorUserId, string? changeSummary, CancellationToken ct)
    {
        EvidenceDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string number = await numbers.NextAsync(SequenceKey, Prefix, innerCt);
            EvidenceRecord entity = EvidenceRecord.Create(
                number, title, ownerUserId, sourceType, evidenceType, classification,
                capturedAtUtc ?? clock.UtcNow, clock.UtcNow, description, sourceRecordId, validFrom, validTo);
            db.EvidenceRecords.Add(entity);
            if (attachmentId is Guid aid && aid != Guid.Empty)
            {
                EvidenceVersion version = EvidenceVersion.Create(entity.Id, 1, aid, actorUserId, clock.UtcNow, changeSummary);
                db.EvidenceVersions.Add(version);
                entity.SetCurrentVersion(version.Id, clock.UtcNow);
                await businessAudit.AppendAsync(EvidenceAudit.Created(entity.Id, entity.EvidenceNumber), innerCt);
                await db.SaveChangesAsync(innerCt);
                created = Map(entity, version, clock.UtcNow);
            }
            else
            {
                await businessAudit.AppendAsync(EvidenceAudit.Created(entity.Id, entity.EvidenceNumber), innerCt);
                await db.SaveChangesAsync(innerCt);
                created = Map(entity, null, clock.UtcNow);
            }
        }, ct);
        return created!;
    }

    public async Task<EvidenceDto> UpdateAsync(
        Guid id, string title, string? description, EvidenceType evidenceType, EvidenceClassification classification,
        DateTimeOffset? validFrom, DateTimeOffset? validTo, CancellationToken ct)
    {
        EvidenceRecord entity = await Load(id, ct);
        entity.UpdateMetadata(title, description, evidenceType, classification, validFrom, validTo, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(EvidenceAudit.Field(entity.Id, entity.EvidenceNumber, "Title", null, title), ct);
        return (await GetAsync(id, true, ct))!;
    }

    public async Task<EvidenceVersionDto> AddVersionAsync(
        Guid id, Guid attachmentId, Guid actorUserId, string? changeSummary, bool supersedeAccepted, CancellationToken ct)
    {
        EvidenceRecord entity = await Load(id, ct);
        if (entity.Status is EvidenceStatus.Withdrawn or EvidenceStatus.Superseded)
            throw new InvalidOperationException("Cannot add versions to withdrawn/superseded evidence.");

        if (supersedeAccepted && entity.Status is EvidenceStatus.Accepted or EvidenceStatus.Expired)
            entity.StartRevision(clock.UtcNow);
        else if (entity.Status is not (EvidenceStatus.Draft or EvidenceStatus.Submitted))
            throw new InvalidOperationException("Cannot add a version in the current status.");

        int next = await db.EvidenceVersions.Where(x => x.EvidenceId == id).MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0;
        next++;
        EvidenceVersion version = EvidenceVersion.Create(
            id, next, attachmentId, actorUserId, clock.UtcNow, changeSummary, entity.CurrentVersionId);
        db.EvidenceVersions.Add(version);
        entity.SetCurrentVersion(version.Id, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            EvidenceAudit.Field(entity.Id, entity.EvidenceNumber, "Version", null, next.ToString()), ct);
        return MapVersion(version);
    }

    public async Task AttachCurrentAsync(Guid id, Guid attachmentId, Guid actorUserId, string? changeSummary, CancellationToken ct)
    {
        EvidenceRecord entity = await Load(id, ct);
        if (entity.Status is not (EvidenceStatus.Draft or EvidenceStatus.Submitted))
            throw new InvalidOperationException("Can only attach files to draft/submitted evidence.");
        if (entity.CurrentVersionId is Guid existing)
        {
            EvidenceVersion? cur = await db.EvidenceVersions.FirstOrDefaultAsync(x => x.Id == existing, ct);
            if (cur is not null && entity.Status != EvidenceStatus.Draft)
                throw new InvalidOperationException("Submitted evidence versions are immutable; create a new version after return to draft.");
        }

        int next = await db.EvidenceVersions.Where(x => x.EvidenceId == id).MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0;
        next++;
        EvidenceVersion version = EvidenceVersion.Create(
            id, next, attachmentId, actorUserId, clock.UtcNow, changeSummary, entity.CurrentVersionId);
        db.EvidenceVersions.Add(version);
        entity.SetCurrentVersion(version.Id, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            EvidenceAudit.Field(entity.Id, entity.EvidenceNumber, "Attachment", null, attachmentId.ToString()), ct);
    }

    public async Task<IReadOnlyList<EvidenceVersionDto>> ListVersionsAsync(Guid id, CancellationToken ct)
    {
        List<EvidenceVersion> items = await db.EvidenceVersions.AsNoTracking()
            .Where(x => x.EvidenceId == id).OrderByDescending(x => x.VersionNumber).ToListAsync(ct);
        return items.Select(MapVersion).ToList();
    }

    public async Task SubmitAsync(Guid id, CancellationToken ct)
    {
        EvidenceRecord entity = await Load(id, ct);
        string old = entity.Status.ToString();
        entity.Submit(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            EvidenceAudit.Field(entity.Id, entity.EvidenceNumber, "Status", old, entity.Status.ToString(), BusinessAuditAction.StatusChanged), ct);
    }

    public async Task ReturnToDraftAsync(Guid id, CancellationToken ct)
    {
        EvidenceRecord entity = await Load(id, ct);
        string old = entity.Status.ToString();
        entity.ReturnToDraft(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            EvidenceAudit.Field(entity.Id, entity.EvidenceNumber, "Status", old, entity.Status.ToString(), BusinessAuditAction.StatusChanged), ct);
    }

    public async Task AcceptAsync(Guid id, Guid acceptorUserId, CancellationToken ct)
    {
        EvidenceRecord entity = await Load(id, ct);
        string old = entity.Status.ToString();
        entity.Accept(acceptorUserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            EvidenceAudit.Field(entity.Id, entity.EvidenceNumber, "Status", old, entity.Status.ToString(), BusinessAuditAction.StatusChanged), ct);
    }

    public async Task WithdrawAsync(Guid id, string reason, CancellationToken ct)
    {
        EvidenceRecord entity = await Load(id, ct);
        string old = entity.Status.ToString();
        entity.Withdraw(reason, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            EvidenceAudit.Field(entity.Id, entity.EvidenceNumber, "Status", old, entity.Status.ToString(), BusinessAuditAction.StatusChanged, reason), ct);
    }

    public async Task LinkAsync(Guid evidenceId, EvidenceLinkTargetType targetType, Guid targetId, Guid actorUserId, CancellationToken ct)
    {
        await Ensure(evidenceId, ct);
        bool exists = await db.EvidenceLinks.AnyAsync(
            x => x.EvidenceId == evidenceId && x.TargetType == targetType && x.TargetId == targetId, ct);
        if (exists) return;
        db.EvidenceLinks.Add(EvidenceLink.Create(evidenceId, targetType, targetId, actorUserId, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            EvidenceAudit.Field(evidenceId, null, "Link", null, $"{targetType}:{targetId}", BusinessAuditAction.Linked), ct);
    }

    public async Task UnlinkAsync(Guid evidenceId, Guid linkId, CancellationToken ct)
    {
        EvidenceLink? link = await db.EvidenceLinks.FirstOrDefaultAsync(x => x.Id == linkId && x.EvidenceId == evidenceId, ct);
        if (link is null) return;
        string old = $"{link.TargetType}:{link.TargetId}";
        db.EvidenceLinks.Remove(link);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            EvidenceAudit.Field(evidenceId, null, "Link", old, null, BusinessAuditAction.Unlinked), ct);
    }

    public async Task<IReadOnlyList<EvidenceLinkDto>> ListLinksAsync(Guid evidenceId, CancellationToken ct)
    {
        List<EvidenceLink> items = await db.EvidenceLinks.AsNoTracking()
            .Where(x => x.EvidenceId == evidenceId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        return items.Select(x => new EvidenceLinkDto(x.Id, x.EvidenceId, x.TargetType.ToString(), x.TargetId, x.CreatedByUserId, x.CreatedAtUtc)).ToList();
    }

    public async Task<IReadOnlyList<EvidenceDto>> ListLinkedToAsync(
        EvidenceLinkTargetType targetType, Guid targetId, bool includeConfidential, CancellationToken ct)
    {
        List<Guid> evidenceIds = await db.EvidenceLinks.AsNoTracking()
            .Where(x => x.TargetType == targetType && x.TargetId == targetId)
            .Select(x => x.EvidenceId).Distinct().ToListAsync(ct);
        if (evidenceIds.Count == 0) return [];
        IQueryable<EvidenceRecord> q = db.EvidenceRecords.AsNoTracking().Where(x => evidenceIds.Contains(x.Id));
        if (!includeConfidential) q = q.Where(x => x.Classification == EvidenceClassification.Internal);
        List<EvidenceRecord> items = await q.OrderByDescending(x => x.UpdatedAtUtc).ToListAsync(ct);
        Dictionary<Guid, EvidenceVersion> versions = await LoadVersions(
            items.Where(x => x.CurrentVersionId.HasValue).Select(x => x.CurrentVersionId!.Value).ToList(), ct);
        DateTimeOffset now = clock.UtcNow;
        return items.Select(x => Map(x, x.CurrentVersionId is Guid vid && versions.TryGetValue(vid, out EvidenceVersion? v) ? v : null, now)).ToList();
    }

    public async Task<EvidenceDto> PromoteAsync(
        string title, EvidenceSourceType sourceType, Guid sourceRecordId, Guid ownerUserId,
        EvidenceType evidenceType, EvidenceClassification classification, string? description,
        DateTimeOffset? validFrom, DateTimeOffset? validTo, Guid? attachmentId, Guid actorUserId,
        EvidenceLinkTargetType? autoLinkType, CancellationToken ct)
    {
        // Prevent accidental duplicate promotion for same source when draft/submitted already exists
        EvidenceRecord? existing = await db.EvidenceRecords.AsNoTracking()
            .Where(x => x.SourceType == sourceType && x.SourceRecordId == sourceRecordId
                && (x.Status == EvidenceStatus.Draft || x.Status == EvidenceStatus.Submitted || x.Status == EvidenceStatus.Accepted))
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
            throw new InvalidOperationException($"Evidence {existing.EvidenceNumber} already exists for this source record.");

        EvidenceDto created = await CreateAsync(
            title, ownerUserId, sourceType, evidenceType, classification, clock.UtcNow, description,
            sourceRecordId, validFrom, validTo, attachmentId, actorUserId, $"Promoted from {sourceType}", ct);

        if (autoLinkType is EvidenceLinkTargetType linkType)
            await LinkAsync(created.Id, linkType, sourceRecordId, actorUserId, ct);

        await businessAudit.AppendAsync(
            EvidenceAudit.Field(created.Id, created.EvidenceNumber, "PromotedFrom", null, $"{sourceType}:{sourceRecordId}"), ct);
        return (await GetAsync(created.Id, true, ct))!;
    }

    public async Task<int> MarkExpiredJobAsync(CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        List<EvidenceRecord> due = await db.EvidenceRecords
            .Where(x => x.Status == EvidenceStatus.Accepted && x.ValidTo != null && x.ValidTo < now)
            .ToListAsync(ct);
        foreach (EvidenceRecord item in due)
        {
            string old = item.Status.ToString();
            item.MarkExpired(now);
            await businessAudit.AppendAsync(
                EvidenceAudit.Field(item.Id, item.EvidenceNumber, "Status", old, item.Status.ToString(), BusinessAuditAction.StatusChanged), ct);
        }

        if (due.Count > 0) await db.SaveChangesAsync(ct);
        return due.Count;
    }

    public async Task<EvidenceCoverageSnapshot> GetForControlsAsync(
        IReadOnlyCollection<Guid> internalControlIds, DateTimeOffset asOfUtc, CancellationToken cancellationToken = default)
    {
        if (internalControlIds.Count == 0)
            return new(0, 0, 0);

        Guid[] controlIds = internalControlIds.Distinct().ToArray();
        List<EvidenceLink> links = await db.EvidenceLinks.AsNoTracking()
            .Where(x => x.TargetType == EvidenceLinkTargetType.InternalControl && controlIds.Contains(x.TargetId))
            .ToListAsync(cancellationToken);
        Guid[] evidenceIds = links.Select(x => x.EvidenceId).Distinct().ToArray();
        List<EvidenceRecord> evidence = evidenceIds.Length == 0
            ? []
            : await db.EvidenceRecords.AsNoTracking().Where(x => evidenceIds.Contains(x.Id)).ToListAsync(cancellationToken);

        Dictionary<Guid, List<EvidenceRecord>> byControl = controlIds.ToDictionary(id => id, _ => new List<EvidenceRecord>());
        foreach (EvidenceLink link in links)
        {
            EvidenceRecord? ev = evidence.FirstOrDefault(e => e.Id == link.EvidenceId);
            if (ev is null) continue;
            byControl[link.TargetId].Add(ev);
        }

        int available = 0, missing = 0, expiredOnly = 0;
        foreach (Guid controlId in controlIds)
        {
            List<EvidenceRecord> list = byControl[controlId];
            bool hasAvailable = list.Any(e =>
                e.Status == EvidenceStatus.Accepted
                && (e.ValidFrom is null || e.ValidFrom <= asOfUtc)
                && (e.ValidTo is null || e.ValidTo >= asOfUtc));
            bool hasExpired = list.Any(e =>
                e.Status == EvidenceStatus.Expired
                || (e.Status == EvidenceStatus.Accepted && e.ValidTo is DateTimeOffset vt && vt < asOfUtc));

            if (hasAvailable) available++;
            else if (hasExpired) expiredOnly++;
            else missing++;
        }

        return new(available, missing, expiredOnly);
    }

    private async Task Ensure(Guid id, CancellationToken ct)
    {
        if (!await db.EvidenceRecords.AnyAsync(x => x.Id == id, ct))
            throw new InvalidOperationException("Evidence was not found.");
    }

    private async Task<EvidenceRecord> Load(Guid id, CancellationToken ct) =>
        await db.EvidenceRecords.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new InvalidOperationException("Evidence was not found.");

    private async Task<Dictionary<Guid, EvidenceVersion>> LoadVersions(List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        return await db.EvidenceVersions.AsNoTracking()
            .Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
    }

    private static EvidenceDto Map(EvidenceRecord x, EvidenceVersion? version, DateTimeOffset now) => new(
        x.Id, x.EvidenceNumber, x.Title, x.Description, x.OwnerUserId,
        x.SourceType.ToString(), x.SourceRecordId, x.EvidenceType.ToString(), x.Classification.ToString(),
        x.ValidFrom, x.ValidTo, x.CapturedAtUtc, x.Status.ToString(),
        x.CurrentVersionId, x.AcceptedByUserId, x.AcceptedAtUtc, x.WithdrawalReason,
        x.CreatedAtUtc, x.UpdatedAtUtc, Convert.ToBase64String(x.RowVersion),
        x.DaysToExpiry(now), x.IsExpired(now), x.IsExpiringSoon(now),
        version?.AttachmentId, version?.VersionNumber);

    private static EvidenceVersionDto MapVersion(EvidenceVersion x) => new(
        x.Id, x.EvidenceId, x.VersionNumber, x.AttachmentId, x.CreatedByUserId, x.CreatedAtUtc, x.ChangeSummary, x.SupersedesVersionId);
}
