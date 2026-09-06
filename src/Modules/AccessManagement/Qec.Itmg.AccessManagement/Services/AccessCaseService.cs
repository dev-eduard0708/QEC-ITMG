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
    Guid? ManagerUserId, Guid? DesignatedApproverUserId, Guid? LinkedTicketId, Guid? VendorId,
    DateTimeOffset? EffectiveAtUtc, string Reason, bool ExistingAccessConfirmed,
    DateTimeOffset? ExistingAccessConfirmedAtUtc, Guid? ExistingAccessConfirmedByUserId,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? ClosedAtUtc,
    string RowVersion, int ItemCount, int PendingMandatoryCount,
    Guid? AccessCategoryId = null,
    string? AccessCategoryKeySnapshot = null,
    string? AccessCategoryNameSnapshot = null,
    bool PreferSubjectEmployeeVerificationSnapshot = false,
    Guid? ApprovedByUserId = null,
    DateTimeOffset? ApprovedAtUtc = null,
    Guid? VerifiedByUserId = null,
    DateTimeOffset? VerifiedAtUtc = null,
    string? VerificationMethod = null,
    string? VerificationOutcome = null,
    string? VerificationComment = null,
    string? FallbackReason = null,
    Guid? ClosedByUserId = null,
    bool IsReadyToClose = false,
    IReadOnlyList<AccessCaseRouteParticipantDto>? RouteParticipants = null);

public sealed record AccessCaseRouteParticipantDto(
    Guid UserId,
    string Stage,
    bool IsSubjectEmployeeDerived);

public sealed record AccessCaseListResult(IReadOnlyList<AccessCaseDto> Items, int TotalCount, int Page, int PageSize);

public sealed record AccessCaseItemDto(
    Guid Id, Guid AccessCaseId, Guid? ConfigurationItemId, string EntitlementKey, string Action,
    bool IsPrivileged, bool IsMandatory, string Status, Guid? FulfilledByUserId,
    DateTimeOffset? FulfilledAtUtc, string? Notes, DateTimeOffset CreatedAtUtc,
    Guid? AccessEntitlementId = null,
    string? NameEn = null,
    string? NameAr = null,
    bool IsCustom = false);

public sealed record AccessCaseItemCreateSpec(
    Guid? AccessEntitlementId,
    string? CustomName,
    AccessItemAction Action,
    string? Notes,
    bool IsSelected = true);

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
    ISharedDbTransaction sharedDbTransaction,
    UserAccessService userAccess)
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
        int page, int pageSize, string? search, AccessCaseType? type, AccessCaseStatus? status, CancellationToken ct,
        Guid? scopedUserId = null,
        string? workQueue = null,
        bool canSeeAll = false)
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

        string queue = (workQueue ?? "all").Trim().ToLowerInvariant();
        if (!canSeeAll || queue is not ("all" or ""))
        {
            if (scopedUserId is null || scopedUserId == Guid.Empty)
                throw new InvalidOperationException("User scope is required.");
            Guid uid = scopedUserId.Value;
            q = queue switch
            {
                "my-requests" or "mine" => q.Where(x => x.RequesterUserId == uid),
                "for-approval" or "approval" => q.Where(x => x.Status == AccessCaseStatus.Approval
                    && db.AccessCaseRouteParticipants.Any(p =>
                        p.AccessCaseId == x.Id && p.Stage == AccessCategoryStage.Approver && p.UserId == uid)),
                "for-fulfillment" or "fulfillment" => q.Where(x => x.Status == AccessCaseStatus.Fulfillment
                    && db.AccessCaseRouteParticipants.Any(p =>
                        p.AccessCaseId == x.Id && p.Stage == AccessCategoryStage.Fulfiller && p.UserId == uid)),
                "for-verification" or "verification" => q.Where(x => x.Status == AccessCaseStatus.Verification
                    && x.VerifiedAtUtc == null
                    && db.AccessCaseRouteParticipants.Any(p =>
                        p.AccessCaseId == x.Id && p.Stage == AccessCategoryStage.Verifier && p.UserId == uid)),
                "for-closure" or "closure" => q.Where(x => x.Status == AccessCaseStatus.Verification
                    && x.VerificationOutcome == AccessVerificationOutcome.Verified
                    && db.AccessCaseRouteParticipants.Any(p =>
                        p.AccessCaseId == x.Id && p.Stage == AccessCategoryStage.Closer && p.UserId == uid)),
                "all" when canSeeAll => q,
                _ => canSeeAll
                    ? q
                    : q.Where(x => x.RequesterUserId == uid
                        || x.SubjectUserId == uid
                        || db.AccessCaseRouteParticipants.Any(p => p.AccessCaseId == x.Id && p.UserId == uid)),
            };
        }

        int total = await q.CountAsync(ct);
        List<AccessCase> items = await q.OrderByDescending(x => x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        Dictionary<Guid, int> counts = await CountItemsAsync(items.Select(x => x.Id).ToList(), ct);
        Dictionary<Guid, int> pendingMandatory = await CountPendingMandatoryAsync(items.Select(x => x.Id).ToList(), ct);
        Dictionary<Guid, List<AccessCaseRouteParticipantDto>> routes = await LoadRoutesAsync(items.Select(x => x.Id).ToList(), ct);
        return new(items.Select(x => Map(x, counts.GetValueOrDefault(x.Id), pendingMandatory.GetValueOrDefault(x.Id),
            routes.GetValueOrDefault(x.Id))).ToList(),
            total, page, pageSize);
    }

    public async Task<AccessCaseDto?> GetAsync(Guid id, CancellationToken ct)
    {
        AccessCase? item = await db.AccessCases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return null;
        int count = await db.AccessCaseItems.CountAsync(x => x.AccessCaseId == id, ct);
        int pending = await db.AccessCaseItems.CountAsync(
            x => x.AccessCaseId == id && x.IsMandatory && x.Status == AccessItemStatus.Pending, ct);
        Dictionary<Guid, List<AccessCaseRouteParticipantDto>> routes = await LoadRoutesAsync([id], ct);
        return Map(item, count, pending, routes.GetValueOrDefault(id));
    }

    public async Task<IReadOnlyList<AccessCaseDto>> ListByVendorAsync(Guid vendorId, CancellationToken ct)
    {
        List<AccessCase> items = await db.AccessCases.AsNoTracking()
            .Where(x => x.VendorId == vendorId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(100)
            .ToListAsync(ct);
        List<Guid> ids = items.Select(x => x.Id).ToList();
        Dictionary<Guid, int> counts = ids.Count == 0
            ? []
            : await db.AccessCaseItems.AsNoTracking().Where(x => ids.Contains(x.AccessCaseId))
                .GroupBy(x => x.AccessCaseId).ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
        Dictionary<Guid, int> pending = ids.Count == 0
            ? []
            : await db.AccessCaseItems.AsNoTracking()
                .Where(x => ids.Contains(x.AccessCaseId) && x.IsMandatory && x.Status == AccessItemStatus.Pending)
                .GroupBy(x => x.AccessCaseId).ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
        return items.Select(x => Map(x, counts.GetValueOrDefault(x.Id), pending.GetValueOrDefault(x.Id))).ToList();
    }

    public async Task<AccessCaseDto> SetVendorAsync(Guid id, Guid? vendorId, CancellationToken ct)
    {
        AccessCase entity = await db.AccessCases.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Access case not found.");
        entity.SetVendorId(vendorId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<AccessCaseDto> CreateAsync(
        AccessCaseType type, Guid requesterUserId, string reason,
        Guid? subjectUserId, string? subjectName, string? subjectEmail,
        Guid? departmentId, Guid? managerUserId, Guid? designatedApproverUserId,
        DateTimeOffset? effectiveAtUtc, CancellationToken ct,
        Guid? accessCategoryId = null,
        bool hasConfigureOverride = false,
        IReadOnlyList<AccessCaseItemCreateSpec>? items = null)
    {
        AccessCaseDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            if (accessCategoryId is Guid catId)
            {
                AccessCategory category = await db.AccessCategories.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == catId, innerCt)
                    ?? throw new InvalidOperationException("Access category not found.");
                if (!category.IsActive)
                    throw new InvalidOperationException("Access category is inactive.");
                bool allowedRequester = await db.AccessCategoryParticipants.AnyAsync(
                    x => x.AccessCategoryId == catId
                        && x.Stage == AccessCategoryStage.Requester
                        && x.UserId == requesterUserId, innerCt);
                if (!allowedRequester)
                {
                    if (!hasConfigureOverride)
                        throw new InvalidOperationException("You are not allowed to request this access category.");
                    await businessAudit.AppendAsync(AccessAudit.Field(
                        catId, category.Key, "RequesterOverride", null, requesterUserId.ToString()), innerCt);
                }
            }

            string number = await numbers.NextAsync(SequenceKey, Prefix, innerCt);
            AccessCase entity = AccessCase.Create(
                number, type, requesterUserId, reason, clock.UtcNow,
                subjectUserId, subjectName, subjectEmail, departmentId, managerUserId,
                designatedApproverUserId, effectiveAtUtc, accessCategoryId);
            db.AccessCases.Add(entity);

            int itemCount = await AddCaseItemsOnCreateAsync(
                entity, type, accessCategoryId, items, innerCt);

            if (type == AccessCaseType.Mover && subjectUserId is Guid moverSubject && moverSubject != Guid.Empty)
                await SnapshotCurrentAccessAsync(entity.Id, moverSubject, innerCt);

            await businessAudit.AppendAsync(AccessAudit.Created(entity.Id, entity.CaseNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            int pendingMandatory = type == AccessCaseType.Leaver ? itemCount : 0;
            created = Map(entity, itemCount, pendingMandatory);
        }, ct);

        return created!;
    }

    private async Task<int> AddCaseItemsOnCreateAsync(
        AccessCase entity,
        AccessCaseType type,
        Guid? accessCategoryId,
        IReadOnlyList<AccessCaseItemCreateSpec>? items,
        CancellationToken ct)
    {
        List<AccessCaseItemCreateSpec> selected = (items ?? [])
            .Where(x => x.IsSelected)
            .ToList();

        if (selected.Count > 0)
        {
            await MaterializeClientItemsAsync(entity, type, selected, ct);
            return selected.Count;
        }

        if (type != AccessCaseType.Leaver)
            return 0;

        if (accessCategoryId is Guid categoryId)
        {
            List<AccessCategoryEntitlement> mappings = await db.AccessCategoryEntitlements.AsNoTracking()
                .Where(x => x.AccessCategoryId == categoryId && x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToListAsync(ct);
            if (mappings.Count > 0)
            {
                List<Guid> entitlementIds = mappings.Select(x => x.AccessEntitlementId).ToList();
                Dictionary<Guid, AccessEntitlement> catalog = await db.AccessEntitlements.AsNoTracking()
                    .Where(x => entitlementIds.Contains(x.Id) && x.IsActive)
                    .ToDictionaryAsync(x => x.Id, ct);
                int added = 0;
                foreach (AccessCategoryEntitlement mapping in mappings)
                {
                    if (!catalog.TryGetValue(mapping.AccessEntitlementId, out AccessEntitlement? entitlement))
                        continue;
                    AccessItemAction action = entitlement.ToItemRevokeAction();
                    db.AccessCaseItems.Add(AccessCaseItem.Create(
                        entity.Id,
                        entitlement.Key,
                        action,
                        clock.UtcNow,
                        isPrivileged: entitlement.IsPrivileged,
                        isMandatory: true,
                        accessEntitlementId: entitlement.Id,
                        entitlementNameEnSnapshot: entitlement.NameEn,
                        entitlementNameArSnapshot: entitlement.NameAr));
                    added++;
                }

                if (added > 0) return added;
            }
        }

        foreach ((string key, AccessItemAction action, bool privileged) in LeaverDefaults)
        {
            db.AccessCaseItems.Add(AccessCaseItem.Create(
                entity.Id, key, action, clock.UtcNow, isPrivileged: privileged, isMandatory: true));
        }

        return LeaverDefaults.Length;
    }

    private async Task MaterializeClientItemsAsync(
        AccessCase entity,
        AccessCaseType type,
        IReadOnlyList<AccessCaseItemCreateSpec> selected,
        CancellationToken ct)
    {
        List<Guid> entitlementIds = selected
            .Where(x => x.AccessEntitlementId is Guid id && id != Guid.Empty)
            .Select(x => x.AccessEntitlementId!.Value)
            .Distinct()
            .ToList();
        Dictionary<Guid, AccessEntitlement> catalog = entitlementIds.Count == 0
            ? []
            : await db.AccessEntitlements.AsNoTracking()
                .Where(x => entitlementIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

        foreach (AccessCaseItemCreateSpec spec in selected)
        {
            ValidateItemActionForType(type, spec.Action);

            if (spec.AccessEntitlementId is Guid entitlementId && entitlementId != Guid.Empty)
            {
                if (!catalog.TryGetValue(entitlementId, out AccessEntitlement? entitlement))
                    throw new InvalidOperationException("Access entitlement not found.");
                if (!entitlement.IsActive)
                    throw new InvalidOperationException($"Access entitlement '{entitlement.Key}' is inactive.");

                AccessItemAction action = ResolveAction(type, spec.Action, entitlement);
                db.AccessCaseItems.Add(AccessCaseItem.Create(
                    entity.Id,
                    entitlement.Key,
                    action,
                    clock.UtcNow,
                    isPrivileged: entitlement.IsPrivileged,
                    isMandatory: type == AccessCaseType.Leaver,
                    notes: spec.Notes,
                    accessEntitlementId: entitlement.Id,
                    entitlementNameEnSnapshot: entitlement.NameEn,
                    entitlementNameArSnapshot: entitlement.NameAr));
                continue;
            }

            if (string.IsNullOrWhiteSpace(spec.CustomName))
                throw new InvalidOperationException("Custom access requires a name when no catalog entitlement is selected.");

            string customName = spec.CustomName.Trim();
            string customKey = "CUSTOM:" + customName.ToUpperInvariant();
            AccessItemAction customAction = ResolveCustomAction(type, spec.Action);
            db.AccessCaseItems.Add(AccessCaseItem.Create(
                entity.Id,
                customKey,
                customAction,
                clock.UtcNow,
                isMandatory: type == AccessCaseType.Leaver,
                notes: spec.Notes,
                entitlementNameEnSnapshot: customName,
                entitlementNameArSnapshot: customName,
                isCustom: true));
            await businessAudit.AppendAsync(AccessAudit.Field(
                entity.Id, entity.CaseNumber, "CustomAccessRequested", null, customName), ct);
        }
    }

    private static void ValidateItemActionForType(AccessCaseType type, AccessItemAction action)
    {
        switch (type)
        {
            case AccessCaseType.Joiner:
            case AccessCaseType.AccessRequest:
                if (action != AccessItemAction.Grant)
                    throw new InvalidOperationException($"{type} cases only support Grant actions.");
                break;
            case AccessCaseType.Mover:
                if (action is not (AccessItemAction.Grant or AccessItemAction.Remove or AccessItemAction.Disable))
                    throw new InvalidOperationException("Mover cases support Grant, Remove, or Disable actions.");
                break;
            case AccessCaseType.Leaver:
                if (action is not (AccessItemAction.Remove or AccessItemAction.Disable or AccessItemAction.Reassign))
                    throw new InvalidOperationException("Leaver cases support Remove, Disable, or Reassign actions.");
                break;
        }
    }

    private static AccessItemAction ResolveAction(
        AccessCaseType type,
        AccessItemAction requested,
        AccessEntitlement entitlement)
    {
        if (type is AccessCaseType.Joiner or AccessCaseType.AccessRequest)
            return AccessItemAction.Grant;
        if (type == AccessCaseType.Leaver)
            return entitlement.ToItemRevokeAction();
        if (type == AccessCaseType.Mover
            && requested is AccessItemAction.Remove or AccessItemAction.Disable)
        {
            // Prefer catalog default when client sends Remove as a generic revoke intent
            if (requested == AccessItemAction.Remove)
                return entitlement.ToItemRevokeAction();
            return requested;
        }

        return requested;
    }

    private static AccessItemAction ResolveCustomAction(AccessCaseType type, AccessItemAction requested)
    {
        if (type is AccessCaseType.Joiner or AccessCaseType.AccessRequest)
            return AccessItemAction.Grant;
        if (type == AccessCaseType.Leaver && requested is AccessItemAction.Grant)
            return AccessItemAction.Remove;
        return requested;
    }

    private async Task SnapshotCurrentAccessAsync(Guid caseId, Guid subjectUserId, CancellationToken ct)
    {
        IReadOnlyList<UserAccessEntitlementDto> current = await userAccess.GetCurrentAccessAsync(
            subjectUserId, ct, activeOnly: true);
        foreach (UserAccessEntitlementDto row in current)
        {
            db.ExistingAccessSnapshotItems.Add(ExistingAccessSnapshotItem.Create(
                caseId,
                row.EntitlementKey,
                clock.UtcNow,
                accessSummary: row.NameEn));
        }
    }

    public async Task<AccessCaseDto> UpdateDraftAsync(
        Guid id, string reason, Guid? subjectUserId, string? subjectName, string? subjectEmail,
        Guid? departmentId, Guid? managerUserId, Guid? designatedApproverUserId,
        DateTimeOffset? effectiveAtUtc, CancellationToken ct,
        Guid? accessCategoryId = null)
    {
        AccessCase entity = await db.AccessCases.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Access case not found.");
        entity.UpdateDraft(reason, subjectUserId, subjectName, subjectEmail, departmentId, managerUserId,
            designatedApproverUserId, effectiveAtUtc, clock.UtcNow, accessCategoryId);
        await businessAudit.AppendAsync(AccessAudit.Field(entity.Id, entity.CaseNumber, "Reason", null, reason), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<AccessCaseDto> SubmitAsync(Guid id, Guid actorUserId, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        if (entity.Status != AccessCaseStatus.Draft)
            throw new InvalidOperationException("Only draft cases can be submitted.");
        if (entity.AccessCategoryId is null)
            throw new InvalidOperationException("Access category is required before submission.");

        AccessCategory category = await db.AccessCategories.AsNoTracking()
            .FirstAsync(x => x.Id == entity.AccessCategoryId, ct);
        if (!category.IsActive)
            throw new InvalidOperationException("Access category is inactive.");

        bool allowedRequester = await db.AccessCategoryParticipants.AnyAsync(
            x => x.AccessCategoryId == category.Id
                && x.Stage == AccessCategoryStage.Requester
                && x.UserId == actorUserId, ct);
        if (!allowedRequester && actorUserId != entity.RequesterUserId)
            throw new InvalidOperationException("Only the requester can submit this case.");
        if (!allowedRequester)
            throw new InvalidOperationException("You are not allowed to request this access category.");

        // Snapshot routing for durability
        List<AccessCaseRouteParticipant> old = await db.AccessCaseRouteParticipants
            .Where(x => x.AccessCaseId == entity.Id).ToListAsync(ct);
        db.AccessCaseRouteParticipants.RemoveRange(old);

        List<AccessCategoryParticipant> configured = await db.AccessCategoryParticipants
            .Where(x => x.AccessCategoryId == category.Id).ToListAsync(ct);
        foreach (AccessCategoryParticipant part in configured)
        {
            db.AccessCaseRouteParticipants.Add(AccessCaseRouteParticipant.Create(
                entity.Id, part.Stage, part.UserId, clock.UtcNow));
        }

        // Preferred subject employee verifier (not for Leaver)
        if (category.PreferSubjectEmployeeVerification
            && entity.Type != AccessCaseType.Leaver
            && entity.SubjectUserId is Guid subjectId)
        {
            bool already = configured.Any(x =>
                x.Stage == AccessCategoryStage.Verifier && x.UserId == subjectId);
            if (!already)
            {
                db.AccessCaseRouteParticipants.Add(AccessCaseRouteParticipant.Create(
                    entity.Id, AccessCategoryStage.Verifier, subjectId, clock.UtcNow,
                    isSubjectEmployeeDerived: true));
            }
            else
            {
                // Mark existing verifier row as subject-derived if it matches subject
                // (already snapshotted as configured; add derived flag via extra row not allowed by unique)
            }
        }

        entity.SetCategorySnapshot(
            category.Id, category.Key, category.NameEn, category.PreferSubjectEmployeeVerification, clock.UtcNow);

        AccessCaseStatus from = entity.Status;
        entity.TransitionTo(AccessCaseStatus.Submitted, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "CaseSubmitted", from.ToString(), nameof(AccessCaseStatus.Submitted),
            BusinessAuditAction.StatusChanged), ct);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "RoutingSnapshotted", null, category.Key), ct);

        entity.TransitionTo(AccessCaseStatus.Approval, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "Status", nameof(AccessCaseStatus.Submitted), nameof(AccessCaseStatus.Approval),
            BusinessAuditAction.StatusChanged), ct);

        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<AccessCaseDto> StartApprovalAsync(Guid id, CancellationToken ct) =>
        await TransitionAsync(id, AccessCaseStatus.Approval, ct);

    public async Task<AccessCaseDto> ApproveAsync(Guid id, Guid actorUserId, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        if (entity.Status != AccessCaseStatus.Approval)
            throw new InvalidOperationException("Case is not awaiting approval.");
        if (actorUserId == entity.RequesterUserId)
            throw new InvalidOperationException("Requester cannot approve their own access case.");
        await EnsureRouteActorAsync(entity.Id, AccessCategoryStage.Approver, actorUserId, ct,
            legacyDesignatedApprover: entity.DesignatedApproverUserId);

        await EnsureSodClearOrExceptionAsync(entity, ct);
        if (entity.Type == AccessCaseType.Mover && !entity.ExistingAccessConfirmed)
            throw new InvalidOperationException("Mover cases require existing-access confirmation before fulfillment.");

        entity.RecordApproval(actorUserId, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "CaseApproved", null, actorUserId.ToString()), ct);
        return await TransitionAsync(id, AccessCaseStatus.Fulfillment, ct, actorUserId);
    }

    public async Task<AccessCaseDto> RejectAsync(Guid id, Guid actorUserId, string? reason, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        if (actorUserId == entity.RequesterUserId)
            throw new InvalidOperationException("Requester cannot reject their own access case.");
        await EnsureRouteActorAsync(entity.Id, AccessCategoryStage.Approver, actorUserId, ct,
            legacyDesignatedApprover: entity.DesignatedApproverUserId);
        AccessCaseStatus from = entity.Status;
        entity.TransitionTo(AccessCaseStatus.Rejected, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "CaseRejected", from.ToString(), nameof(AccessCaseStatus.Rejected),
            BusinessAuditAction.StatusChanged, reason), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<AccessCaseDto> StartVerificationAsync(Guid id, Guid actorUserId, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        await EnsureRouteActorAsync(entity.Id, AccessCategoryStage.Fulfiller, actorUserId, ct);
        await EnsureMandatoryCompleteOrExceptionAsync(entity, ct);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "SentForVerification", null, actorUserId.ToString()), ct);
        return await TransitionAsync(id, AccessCaseStatus.Verification, ct, actorUserId);
    }

    public async Task<AccessCaseDto> VerifyEmployeeAsync(Guid id, Guid actorUserId, bool everythingWorks, string? comment, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        if (entity.Status != AccessCaseStatus.Verification)
            throw new InvalidOperationException("Case is not awaiting verification.");
        if (entity.SubjectUserId != actorUserId)
            throw new InvalidOperationException("Only the subject employee can complete employee verification.");
        if (entity.Type == AccessCaseType.Leaver)
            throw new InvalidOperationException("Leaver cases do not use subject employee verification.");

        if (!everythingWorks)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(comment);
            entity.ClearVerification(clock.UtcNow);
            entity.RecordVerification(
                actorUserId, AccessVerificationMethod.Employee, AccessVerificationOutcome.Problem,
                clock.UtcNow, comment);
            entity.TransitionTo(AccessCaseStatus.Fulfillment, clock.UtcNow);
            await businessAudit.AppendAsync(AccessAudit.Field(
                entity.Id, entity.CaseNumber, "VerificationFailed", nameof(AccessCaseStatus.Verification),
                nameof(AccessCaseStatus.Fulfillment), BusinessAuditAction.StatusChanged, comment), ct);
            await db.SaveChangesAsync(ct);
            return (await GetAsync(id, ct))!;
        }

        entity.RecordVerification(
            actorUserId, AccessVerificationMethod.Employee, AccessVerificationOutcome.Verified, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "EmployeeVerified", null, actorUserId.ToString()), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<AccessCaseDto> VerifyFallbackAsync(Guid id, Guid actorUserId, string fallbackReason, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        if (entity.Status != AccessCaseStatus.Verification)
            throw new InvalidOperationException("Case is not awaiting verification.");
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackReason);
        await EnsureRouteActorAsync(entity.Id, AccessCategoryStage.Verifier, actorUserId, ct,
            allowSubjectDerivedOnly: false);

        // Fallback verifiers are configured Verifier participants who are NOT the subject-derived-only path
        bool isConfiguredFallback = await db.AccessCaseRouteParticipants.AnyAsync(
            x => x.AccessCaseId == id
                && x.Stage == AccessCategoryStage.Verifier
                && x.UserId == actorUserId
                && !x.IsSubjectEmployeeDerived, ct);
        if (!isConfiguredFallback && entity.AccessCategoryId is not null)
        {
            // Legacy / missing snapshot: allow if category still has this verifier
            isConfiguredFallback = await db.AccessCategoryParticipants.AnyAsync(
                x => x.AccessCategoryId == entity.AccessCategoryId
                    && x.Stage == AccessCategoryStage.Verifier
                    && x.UserId == actorUserId, ct);
        }

        if (!isConfiguredFallback)
            throw new InvalidOperationException("You are not an authorized fallback verifier for this case.");

        entity.RecordVerification(
            actorUserId, AccessVerificationMethod.Fallback, AccessVerificationOutcome.Verified,
            clock.UtcNow, fallbackReason: fallbackReason);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "FallbackVerified", null, actorUserId.ToString(),
            BusinessAuditAction.Updated, fallbackReason), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<AccessCaseDto> CloseAsync(Guid id, Guid actorUserId, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        if (!entity.IsReadyToClose)
            throw new InvalidOperationException("Case must be successfully verified before closure.");
        await EnsureRouteActorAsync(entity.Id, AccessCategoryStage.Closer, actorUserId, ct);
        await EnsureMandatoryCompleteOrExceptionAsync(entity, ct);
        entity.RecordClosure(actorUserId, clock.UtcNow);
        AccessCaseStatus from = entity.Status;
        entity.TransitionTo(AccessCaseStatus.Closed, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "CaseClosed", null, actorUserId.ToString()), ct);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "Status", from.ToString(), nameof(AccessCaseStatus.Closed),
            BusinessAuditAction.StatusChanged), ct);
        await userAccess.ReconcileFromClosedCaseAsync(entity, ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
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
        AccessCase entity = await LoadTrackedAsync(caseId, ct);
        if (entity.Status != AccessCaseStatus.Fulfillment)
            throw new InvalidOperationException("Items can only be fulfilled while the case is in fulfillment.");
        await EnsureRouteActorAsync(caseId, AccessCategoryStage.Fulfiller, userId, ct);
        AccessCaseItem item = await db.AccessCaseItems.FirstOrDefaultAsync(x => x.Id == itemId && x.AccessCaseId == caseId, ct)
            ?? throw new InvalidOperationException("Access case item not found.");
        item.MarkCompleted(userId, clock.UtcNow, notes);
        await businessAudit.AppendAsync(AccessAudit.Field(
            caseId, entity.CaseNumber, "AccessItemFulfilled", item.EntitlementKey, nameof(AccessItemStatus.Completed)), ct);
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

    private async Task EnsureRouteActorAsync(
        Guid caseId,
        AccessCategoryStage stage,
        Guid actorUserId,
        CancellationToken ct,
        Guid? legacyDesignatedApprover = null,
        bool allowSubjectDerivedOnly = true)
    {
        IQueryable<AccessCaseRouteParticipant> q = db.AccessCaseRouteParticipants.AsNoTracking()
            .Where(x => x.AccessCaseId == caseId && x.Stage == stage && x.UserId == actorUserId);
        if (!allowSubjectDerivedOnly && stage == AccessCategoryStage.Verifier)
            q = q.Where(x => !x.IsSubjectEmployeeDerived);

        bool allowed = await q.AnyAsync(ct);
        if (allowed) return;

        // Legacy cases without routing snapshot
        bool hasSnapshot = await db.AccessCaseRouteParticipants.AsNoTracking()
            .AnyAsync(x => x.AccessCaseId == caseId, ct);
        if (!hasSnapshot)
        {
            if (stage == AccessCategoryStage.Approver
                && legacyDesignatedApprover is Guid designated
                && designated == actorUserId)
                return;
            if (stage is AccessCategoryStage.Fulfiller or AccessCategoryStage.Closer or AccessCategoryStage.Verifier)
                return; // legacy path relies on permission only
        }

        throw new InvalidOperationException($"You are not an authorized {stage.ToString().ToLowerInvariant()} for this case.");
    }

    private async Task<Dictionary<Guid, List<AccessCaseRouteParticipantDto>>> LoadRoutesAsync(
        List<Guid> caseIds, CancellationToken ct)
    {
        if (caseIds.Count == 0) return [];
        List<AccessCaseRouteParticipant> rows = await db.AccessCaseRouteParticipants.AsNoTracking()
            .Where(x => caseIds.Contains(x.AccessCaseId)).ToListAsync(ct);
        return rows.GroupBy(x => x.AccessCaseId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new AccessCaseRouteParticipantDto(x.UserId, x.Stage.ToString(), x.IsSubjectEmployeeDerived)).ToList());
    }

    private static AccessCaseDto Map(
        AccessCase x,
        int itemCount,
        int pendingMandatory,
        IReadOnlyList<AccessCaseRouteParticipantDto>? routes = null) =>
        new(x.Id, x.CaseNumber, x.Type.ToString(), x.Status.ToString(), x.RequesterUserId,
            x.SubjectUserId, x.SubjectName, x.SubjectEmail, x.DepartmentId, x.ManagerUserId,
            x.DesignatedApproverUserId, x.LinkedTicketId, x.VendorId, x.EffectiveAtUtc, x.Reason,
            x.ExistingAccessConfirmed, x.ExistingAccessConfirmedAtUtc, x.ExistingAccessConfirmedByUserId,
            x.CreatedAtUtc, x.UpdatedAtUtc, x.ClosedAtUtc, Convert.ToBase64String(x.RowVersion),
            itemCount, pendingMandatory,
            x.AccessCategoryId, x.AccessCategoryKeySnapshot, x.AccessCategoryNameSnapshot,
            x.PreferSubjectEmployeeVerificationSnapshot,
            x.ApprovedByUserId, x.ApprovedAtUtc,
            x.VerifiedByUserId, x.VerifiedAtUtc,
            x.VerificationMethod?.ToString(), x.VerificationOutcome?.ToString(),
            x.VerificationComment, x.FallbackReason, x.ClosedByUserId,
            x.IsReadyToClose, routes);

    private static AccessCaseItemDto Map(AccessCaseItem x) =>
        new(x.Id, x.AccessCaseId, x.ConfigurationItemId, x.EntitlementKey, x.Action.ToString(),
            x.IsPrivileged, x.IsMandatory, x.Status.ToString(), x.FulfilledByUserId, x.FulfilledAtUtc,
            x.Notes, x.CreatedAtUtc,
            x.AccessEntitlementId,
            x.EntitlementNameEnSnapshot ?? x.EntitlementKey,
            x.EntitlementNameArSnapshot ?? x.EntitlementNameEnSnapshot ?? x.EntitlementKey,
            x.IsCustom);
}
