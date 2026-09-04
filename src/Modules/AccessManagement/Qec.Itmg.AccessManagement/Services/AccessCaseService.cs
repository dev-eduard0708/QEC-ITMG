using Microsoft.EntityFrameworkCore;
using Qec.Itmg.AccessManagement.Domain;
using Qec.Itmg.AccessManagement.Persistence;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Numbering;

namespace Qec.Itmg.AccessManagement.Services;

public sealed record AccessCaseDto(
    Guid Id, string CaseNumber, string Type, string Status, Guid RequesterUserId,
    Guid? SubjectUserId, string? SubjectName, string? SubjectEmail, Guid? DepartmentId,
    Guid? ManagerUserId, Guid? DesignatedApproverUserId, Guid? LinkedTicketId,
    DateTimeOffset? EffectiveAtUtc, string Reason, bool ExistingAccessConfirmed,
    DateTimeOffset? ExistingAccessConfirmedAtUtc, Guid? ExistingAccessConfirmedByUserId,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? ClosedAtUtc,
    string RowVersion, int ItemCount, int PendingMandatoryCount);

public sealed record AccessCaseListResult(IReadOnlyList<AccessCaseDto> Items, int TotalCount, int Page, int PageSize);

public sealed record AccessCaseItemDto(
    Guid Id, Guid AccessCaseId, Guid? ConfigurationItemId, string EntitlementKey, string Action,
    bool IsPrivileged, bool IsMandatory, string Status, Guid? FulfilledByUserId,
    DateTimeOffset? FulfilledAtUtc, string? Notes, DateTimeOffset CreatedAtUtc);

public sealed record ExistingAccessItemDto(
    Guid Id, Guid AccessCaseId, Guid? ConfigurationItemId, string EntitlementKey,
    string? AccessSummary, DateTimeOffset CreatedAtUtc);

public sealed record AccessCaseExceptionDto(
    Guid Id, Guid AccessCaseId, string Type, string Reason, Guid AuthorizedByUserId,
    Guid? RelatedSodRuleId, DateTimeOffset CreatedAtUtc);

public sealed record SodViolationDto(Guid RuleId, string RuleName, string LeftEntitlementKey, string RightEntitlementKey, string Severity);

internal static class AccessAudit
{
    public static BusinessAuditEntry Created(Guid id, string number) => new()
    {
        AggregateType = AuditAggregateType.Access,
        AggregateId = id,
        BusinessNumber = number,
        Action = BusinessAuditAction.Created,
        Source = AuditSource.Api,
    };

    public static BusinessAuditEntry Field(
        Guid id, string? number, string field, string? oldValue, string? newValue,
        BusinessAuditAction action = BusinessAuditAction.Updated, string? reason = null) => new()
    {
        AggregateType = AuditAggregateType.Access,
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

public sealed class AccessCaseService(
    AccessManagementDbContext db,
    INumberSequenceService numbers,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction)
{
    public const string SequenceKey = "access";
    public const string Prefix = "AC";

    private static readonly (string Key, AccessItemAction Action, bool Privileged)[] LeaverDefaults =
    [
        ("Directory/AD disable", AccessItemAction.Disable, false),
        ("Mailbox handling", AccessItemAction.Disable, false),
        ("VPN removal", AccessItemAction.Remove, false),
        ("Application access removal", AccessItemAction.Remove, false),
        ("Privileged access removal", AccessItemAction.Remove, true),
        ("Asset recovery/reference", AccessItemAction.Remove, false),
        ("Service-account ownership reassignment", AccessItemAction.Reassign, false),
    ];

    public async Task<AccessCaseListResult> ListAsync(
        int page, int pageSize, string? search, AccessCaseType? type, AccessCaseStatus? status, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<AccessCase> q = db.AccessCases.AsNoTracking();
        if (type is AccessCaseType t) q = q.Where(x => x.Type == t);
        if (status is AccessCaseStatus s) q = q.Where(x => x.Status == s);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(x => x.CaseNumber.Contains(term) || x.Reason.Contains(term)
                || (x.SubjectName != null && x.SubjectName.Contains(term))
                || (x.SubjectEmail != null && x.SubjectEmail.Contains(term)));
        }

        int total = await q.CountAsync(ct);
        List<AccessCase> items = await q.OrderByDescending(x => x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        Dictionary<Guid, int> counts = await CountItemsAsync(items.Select(x => x.Id).ToList(), ct);
        Dictionary<Guid, int> pendingMandatory = await CountPendingMandatoryAsync(items.Select(x => x.Id).ToList(), ct);
        return new(items.Select(x => Map(x, counts.GetValueOrDefault(x.Id), pendingMandatory.GetValueOrDefault(x.Id))).ToList(),
            total, page, pageSize);
    }

    public async Task<AccessCaseDto?> GetAsync(Guid id, CancellationToken ct)
    {
        AccessCase? item = await db.AccessCases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return null;
        int count = await db.AccessCaseItems.CountAsync(x => x.AccessCaseId == id, ct);
        int pending = await db.AccessCaseItems.CountAsync(
            x => x.AccessCaseId == id && x.IsMandatory && x.Status == AccessItemStatus.Pending, ct);
        return Map(item, count, pending);
    }

    public async Task<AccessCaseDto> CreateAsync(
        AccessCaseType type, Guid requesterUserId, string reason,
        Guid? subjectUserId, string? subjectName, string? subjectEmail,
        Guid? departmentId, Guid? managerUserId, Guid? designatedApproverUserId,
        DateTimeOffset? effectiveAtUtc, CancellationToken ct)
    {
        AccessCaseDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string number = await numbers.NextAsync(SequenceKey, Prefix, innerCt);
            AccessCase entity = AccessCase.Create(
                number, type, requesterUserId, reason, clock.UtcNow,
                subjectUserId, subjectName, subjectEmail, departmentId, managerUserId,
                designatedApproverUserId, effectiveAtUtc);
            db.AccessCases.Add(entity);

            if (type == AccessCaseType.Leaver)
            {
                foreach ((string key, AccessItemAction action, bool privileged) in LeaverDefaults)
                {
                    db.AccessCaseItems.Add(AccessCaseItem.Create(
                        entity.Id, key, action, clock.UtcNow, isPrivileged: privileged, isMandatory: true));
                }
            }

            await businessAudit.AppendAsync(AccessAudit.Created(entity.Id, entity.CaseNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = Map(entity, type == AccessCaseType.Leaver ? LeaverDefaults.Length : 0,
                type == AccessCaseType.Leaver ? LeaverDefaults.Length : 0);
        }, ct);

        return created!;
    }

    public async Task<AccessCaseDto> UpdateDraftAsync(
        Guid id, string reason, Guid? subjectUserId, string? subjectName, string? subjectEmail,
        Guid? departmentId, Guid? managerUserId, Guid? designatedApproverUserId,
        DateTimeOffset? effectiveAtUtc, CancellationToken ct)
    {
        AccessCase entity = await db.AccessCases.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Access case not found.");
        entity.UpdateDraft(reason, subjectUserId, subjectName, subjectEmail, departmentId, managerUserId,
            designatedApproverUserId, effectiveAtUtc, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(entity.Id, entity.CaseNumber, "Reason", null, reason), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<AccessCaseDto> SubmitAsync(Guid id, CancellationToken ct) =>
        await TransitionAsync(id, AccessCaseStatus.Submitted, ct);

    public async Task<AccessCaseDto> StartApprovalAsync(Guid id, CancellationToken ct) =>
        await TransitionAsync(id, AccessCaseStatus.Approval, ct);

    public async Task<AccessCaseDto> ApproveAsync(Guid id, Guid actorUserId, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        if (entity.Status != AccessCaseStatus.Approval)
            throw new InvalidOperationException("Case is not awaiting approval.");
        if (actorUserId == entity.RequesterUserId)
            throw new InvalidOperationException("Requester cannot approve their own access case.");
        if (entity.DesignatedApproverUserId is Guid designated && designated != actorUserId)
            throw new InvalidOperationException("Only the designated approver can approve this case.");

        await EnsureSodClearOrExceptionAsync(entity, ct);
        if (entity.Type == AccessCaseType.Mover && !entity.ExistingAccessConfirmed)
            throw new InvalidOperationException("Mover cases require existing-access confirmation before fulfillment.");

        return await TransitionAsync(id, AccessCaseStatus.Fulfillment, ct, actorUserId);
    }

    public async Task<AccessCaseDto> RejectAsync(Guid id, Guid actorUserId, string? reason, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        if (actorUserId == entity.RequesterUserId)
            throw new InvalidOperationException("Requester cannot reject their own access case.");
        AccessCaseStatus from = entity.Status;
        entity.TransitionTo(AccessCaseStatus.Rejected, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "Status", from.ToString(), nameof(AccessCaseStatus.Rejected),
            BusinessAuditAction.StatusChanged, reason), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<AccessCaseDto> StartVerificationAsync(Guid id, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        await EnsureMandatoryCompleteOrExceptionAsync(entity, ct);
        return await TransitionAsync(id, AccessCaseStatus.Verification, ct);
    }

    public async Task<AccessCaseDto> CloseAsync(Guid id, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        await EnsureMandatoryCompleteOrExceptionAsync(entity, ct);
        return await TransitionAsync(id, AccessCaseStatus.Closed, ct);
    }

    public async Task<AccessCaseDto> CancelAsync(Guid id, Guid actorUserId, string? reason, bool hasPrivilegedOverride, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        bool overrideNeeded = entity.Type == AccessCaseType.Leaver && entity.Status == AccessCaseStatus.Fulfillment;
        if (overrideNeeded)
        {
            if (!hasPrivilegedOverride)
                throw new InvalidOperationException("Leaver cancellation after fulfillment requires an audited override.");
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            db.AccessCaseExceptions.Add(AccessCaseException.Create(
                entity.Id, AccessCaseExceptionType.CancelOverride, reason!, actorUserId, clock.UtcNow));
            await businessAudit.AppendAsync(AccessAudit.Field(
                entity.Id, entity.CaseNumber, "CancelOverride", null, "Granted",
                BusinessAuditAction.Updated, reason), ct);
        }

        AccessCaseStatus from = entity.Status;
        entity.TransitionTo(AccessCaseStatus.Cancelled, clock.UtcNow, hasCancelOverride: overrideNeeded);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "Status", from.ToString(), nameof(AccessCaseStatus.Cancelled),
            BusinessAuditAction.StatusChanged, reason), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task LinkTicketAsync(Guid id, Guid ticketId, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        entity.LinkTicket(ticketId, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "LinkedTicketId", null, ticketId.ToString(), BusinessAuditAction.Linked), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task ConfirmExistingAccessAsync(Guid id, Guid userId, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        int snapshotCount = await db.ExistingAccessSnapshotItems.CountAsync(x => x.AccessCaseId == id, ct);
        if (snapshotCount == 0)
            throw new InvalidOperationException("Capture at least one existing-access item before confirmation.");
        entity.ConfirmExistingAccess(userId, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "ExistingAccessConfirmed", "false", "true"), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AccessCaseItemDto>> ListItemsAsync(Guid caseId, CancellationToken ct)
    {
        List<AccessCaseItem> items = await db.AccessCaseItems.AsNoTracking()
            .Where(x => x.AccessCaseId == caseId).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<AccessCaseItemDto> AddItemAsync(
        Guid caseId, string entitlementKey, AccessItemAction action, Guid? configurationItemId,
        bool isPrivileged, bool isMandatory, string? notes, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(caseId, ct);
        if (entity.Status is AccessCaseStatus.Closed or AccessCaseStatus.Rejected or AccessCaseStatus.Cancelled)
            throw new InvalidOperationException("Cannot add items to a terminal case.");
        AccessCaseItem item = AccessCaseItem.Create(
            caseId, entitlementKey, action, clock.UtcNow, configurationItemId, isPrivileged, isMandatory, notes);
        db.AccessCaseItems.Add(item);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "ItemAdded", null, entitlementKey), ct);
        await db.SaveChangesAsync(ct);
        return Map(item);
    }

    public async Task<AccessCaseItemDto> CompleteItemAsync(Guid caseId, Guid itemId, Guid userId, string? notes, CancellationToken ct)
    {
        AccessCaseItem item = await db.AccessCaseItems.FirstOrDefaultAsync(x => x.Id == itemId && x.AccessCaseId == caseId, ct)
            ?? throw new InvalidOperationException("Access case item not found.");
        item.MarkCompleted(userId, clock.UtcNow, notes);
        await businessAudit.AppendAsync(AccessAudit.Field(
            caseId, null, "ItemCompleted", item.EntitlementKey, nameof(AccessItemStatus.Completed)), ct);
        await db.SaveChangesAsync(ct);
        return Map(item);
    }

    public async Task<IReadOnlyList<ExistingAccessItemDto>> ListExistingAccessAsync(Guid caseId, CancellationToken ct)
    {
        List<ExistingAccessSnapshotItem> items = await db.ExistingAccessSnapshotItems.AsNoTracking()
            .Where(x => x.AccessCaseId == caseId).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        return items.Select(x => new ExistingAccessItemDto(
            x.Id, x.AccessCaseId, x.ConfigurationItemId, x.EntitlementKey, x.AccessSummary, x.CreatedAtUtc)).ToList();
    }

    public async Task<ExistingAccessItemDto> AddExistingAccessAsync(
        Guid caseId, string entitlementKey, Guid? configurationItemId, string? accessSummary, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(caseId, ct);
        if (entity.Type != AccessCaseType.Mover)
            throw new InvalidOperationException("Existing access snapshots apply to Mover cases only.");
        ExistingAccessSnapshotItem item = ExistingAccessSnapshotItem.Create(
            caseId, entitlementKey, clock.UtcNow, configurationItemId, accessSummary);
        db.ExistingAccessSnapshotItems.Add(item);
        entity.ClearExistingAccessConfirmation(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return new ExistingAccessItemDto(item.Id, item.AccessCaseId, item.ConfigurationItemId, item.EntitlementKey, item.AccessSummary, item.CreatedAtUtc);
    }

    public async Task<AccessCaseExceptionDto> RecordExceptionAsync(
        Guid caseId, AccessCaseExceptionType type, string reason, Guid authorizedByUserId, Guid? sodRuleId, CancellationToken ct)
    {
        _ = await LoadTrackedAsync(caseId, ct);
        AccessCaseException ex = AccessCaseException.Create(caseId, type, reason, authorizedByUserId, clock.UtcNow, sodRuleId);
        db.AccessCaseExceptions.Add(ex);
        await businessAudit.AppendAsync(AccessAudit.Field(
            caseId, null, type.ToString(), null, "Recorded", BusinessAuditAction.Updated, reason), ct);
        await db.SaveChangesAsync(ct);
        return new AccessCaseExceptionDto(ex.Id, ex.AccessCaseId, ex.Type.ToString(), ex.Reason, ex.AuthorizedByUserId, ex.RelatedSodRuleId, ex.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<AccessCaseExceptionDto>> ListExceptionsAsync(Guid caseId, CancellationToken ct)
    {
        List<AccessCaseException> items = await db.AccessCaseExceptions.AsNoTracking()
            .Where(x => x.AccessCaseId == caseId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        return items.Select(x => new AccessCaseExceptionDto(
            x.Id, x.AccessCaseId, x.Type.ToString(), x.Reason, x.AuthorizedByUserId, x.RelatedSodRuleId, x.CreatedAtUtc)).ToList();
    }

    public async Task<IReadOnlyList<SodViolationDto>> DetectSodViolationsAsync(Guid caseId, CancellationToken ct)
    {
        List<string> grants = await db.AccessCaseItems.AsNoTracking()
            .Where(x => x.AccessCaseId == caseId && x.Action == AccessItemAction.Grant)
            .Select(x => x.EntitlementKey)
            .ToListAsync(ct);
        HashSet<string> set = grants.ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<SodRule> rules = await db.SodRules.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        List<SodViolationDto> hits = [];
        foreach (SodRule rule in rules)
        {
            if (set.Contains(rule.LeftEntitlementKey) && set.Contains(rule.RightEntitlementKey))
                hits.Add(new SodViolationDto(rule.Id, rule.Name, rule.LeftEntitlementKey, rule.RightEntitlementKey, rule.Severity));
        }

        return hits;
    }

    private async Task EnsureSodClearOrExceptionAsync(AccessCase entity, CancellationToken ct)
    {
        IReadOnlyList<SodViolationDto> violations = await DetectSodViolationsAsync(entity.Id, ct);
        if (violations.Count == 0) return;
        HashSet<Guid> excepted = (await db.AccessCaseExceptions.AsNoTracking()
            .Where(x => x.AccessCaseId == entity.Id && x.Type == AccessCaseExceptionType.SodException)
            .Select(x => x.RelatedSodRuleId)
            .ToListAsync(ct))
            .Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
        SodViolationDto? open = violations.FirstOrDefault(v => !excepted.Contains(v.RuleId));
        if (open is not null)
            throw new InvalidOperationException(
                $"SoD violation: {open.RuleName} ({open.LeftEntitlementKey} vs {open.RightEntitlementKey}). Record an exception to proceed.");
    }

    private async Task EnsureMandatoryCompleteOrExceptionAsync(AccessCase entity, CancellationToken ct)
    {
        bool pending = await db.AccessCaseItems.AnyAsync(
            x => x.AccessCaseId == entity.Id && x.IsMandatory && x.Status == AccessItemStatus.Pending, ct);
        if (!pending) return;
        bool hasOverride = await db.AccessCaseExceptions.AnyAsync(
            x => x.AccessCaseId == entity.Id && x.Type == AccessCaseExceptionType.MandatoryItemOverride, ct);
        if (!hasOverride)
            throw new InvalidOperationException("Mandatory checklist items must be completed or explicitly overridden before closing.");
    }

    private async Task<AccessCaseDto> TransitionAsync(
        Guid id, AccessCaseStatus next, CancellationToken ct, Guid? actorUserId = null, bool hasCancelOverride = false)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        if (next == AccessCaseStatus.Fulfillment)
        {
            await EnsureSodClearOrExceptionAsync(entity, ct);
            if (entity.Type == AccessCaseType.Mover && !entity.ExistingAccessConfirmed)
                throw new InvalidOperationException("Mover cases require existing-access confirmation before fulfillment.");
        }

        AccessCaseStatus from = entity.Status;
        entity.TransitionTo(next, clock.UtcNow, hasCancelOverride);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "Status", from.ToString(), next.ToString(),
            BusinessAuditAction.StatusChanged), ct);
        await db.SaveChangesAsync(ct);
        _ = actorUserId;
        return (await GetAsync(id, ct))!;
    }

    private async Task<AccessCase> LoadTrackedAsync(Guid id, CancellationToken ct) =>
        await db.AccessCases.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new InvalidOperationException("Access case not found.");

    private async Task<Dictionary<Guid, int>> CountItemsAsync(List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        return await db.AccessCaseItems.AsNoTracking()
            .Where(x => ids.Contains(x.AccessCaseId))
            .GroupBy(x => x.AccessCaseId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    private async Task<Dictionary<Guid, int>> CountPendingMandatoryAsync(List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        return await db.AccessCaseItems.AsNoTracking()
            .Where(x => ids.Contains(x.AccessCaseId) && x.IsMandatory && x.Status == AccessItemStatus.Pending)
            .GroupBy(x => x.AccessCaseId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    private static AccessCaseDto Map(AccessCase x, int itemCount, int pendingMandatory) =>
        new(x.Id, x.CaseNumber, x.Type.ToString(), x.Status.ToString(), x.RequesterUserId,
            x.SubjectUserId, x.SubjectName, x.SubjectEmail, x.DepartmentId, x.ManagerUserId,
            x.DesignatedApproverUserId, x.LinkedTicketId, x.EffectiveAtUtc, x.Reason,
            x.ExistingAccessConfirmed, x.ExistingAccessConfirmedAtUtc, x.ExistingAccessConfirmedByUserId,
            x.CreatedAtUtc, x.UpdatedAtUtc, x.ClosedAtUtc, Convert.ToBase64String(x.RowVersion),
            itemCount, pendingMandatory);

    private static AccessCaseItemDto Map(AccessCaseItem x) =>
        new(x.Id, x.AccessCaseId, x.ConfigurationItemId, x.EntitlementKey, x.Action.ToString(),
            x.IsPrivileged, x.IsMandatory, x.Status.ToString(), x.FulfilledByUserId, x.FulfilledAtUtc,
            x.Notes, x.CreatedAtUtc);
}
