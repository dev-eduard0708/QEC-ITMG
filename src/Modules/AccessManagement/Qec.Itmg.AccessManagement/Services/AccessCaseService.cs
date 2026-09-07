using Microsoft.EntityFrameworkCore;
using Qec.Itmg.AccessManagement.Domain;
using Qec.Itmg.AccessManagement.Persistence;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Numbering;

namespace Qec.Itmg.AccessManagement.Services;

public sealed record AccessCaseActionsDto(
    bool CanSubmit,
    bool CanResubmit,
    bool CanEditRequest,
    bool CanApprove,
    bool CanReject,
    bool CanSendForRework,
    bool CanFulfill,
    bool CanSendForVerification,
    bool CanVerifyAsEmployee,
    bool CanVerifyAsFallback,
    bool CanClose,
    bool IsRoutedApproverMissingPermission);

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
    IReadOnlyList<AccessCaseRouteParticipantDto>? RouteParticipants = null,
    string? AccessCategoryDisplayName = null,
    AccessCaseActionsDto? Actions = null,
    Guid? ReturnedForReworkByUserId = null,
    DateTimeOffset? ReturnedForReworkAtUtc = null,
    string? ReworkReason = null,
    Guid? RejectedByUserId = null,
    DateTimeOffset? RejectedAtUtc = null,
    string? RejectionReason = null,
    int CurrentScopeRevisionNumber = 0);

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

public sealed record AccessCaseRevisionItemDto(
    Guid Id,
    Guid RevisionId,
    Guid? AccessEntitlementId,
    string EntitlementKeySnapshot,
    string? NameEnSnapshot,
    string? NameArSnapshot,
    string? CustomName,
    string Action,
    string? Notes,
    bool IsPrivileged,
    bool IsCustom);

public sealed record AccessCaseRevisionDto(
    Guid Id,
    Guid AccessCaseId,
    int RevisionNumber,
    Guid SubmittedByUserId,
    DateTimeOffset SubmittedAtUtc,
    string Decision,
    Guid? DecidedByUserId,
    DateTimeOffset? DecidedAtUtc,
    string? DecisionReason,
    IReadOnlyList<AccessCaseRevisionItemDto> Items);

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
        Dictionary<Guid, string> categoryNames = await ResolveLiveCategoryNamesAsync(items, ct);
        return new(items.Select(x => Map(x, counts.GetValueOrDefault(x.Id), pendingMandatory.GetValueOrDefault(x.Id),
            routes.GetValueOrDefault(x.Id), categoryNames)).ToList(),
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
        Dictionary<Guid, string> categoryNames = await ResolveLiveCategoryNamesAsync([item], ct);
        return Map(item, count, pending, routes.GetValueOrDefault(id), categoryNames);
    }

    public Task<AccessCaseActionsDto> ResolveActionsAsync(
        AccessCaseDto accessCase,
        Guid actorUserId,
        bool hasAccessRequest,
        bool hasAccessApprove,
        bool hasAccessFulfill,
        CancellationToken ct,
        bool hasAccessConfigure = false)
    {
        _ = ct;
        bool isDraft = StatusEquals(accessCase.Status, AccessCaseStatus.Draft);
        bool isRework = StatusEquals(accessCase.Status, AccessCaseStatus.Rework);
        bool isApproval = StatusEquals(accessCase.Status, AccessCaseStatus.Approval);
        bool isFulfillment = StatusEquals(accessCase.Status, AccessCaseStatus.Fulfillment);
        bool isVerification = StatusEquals(accessCase.Status, AccessCaseStatus.Verification);
        bool isRequester = accessCase.RequesterUserId == actorUserId;
        bool isSubject = accessCase.SubjectUserId == actorUserId;
        bool isLeaver = string.Equals(accessCase.Type, nameof(AccessCaseType.Leaver), StringComparison.OrdinalIgnoreCase);

        bool isSnapshottedApprover = (accessCase.RouteParticipants ?? [])
            .Any(r => StageEquals(r.Stage, AccessCategoryStage.Approver) && r.UserId == actorUserId);
        bool isRoutedApprover = IsRouted(accessCase, AccessCategoryStage.Approver, actorUserId);
        bool isRoutedFulfiller = IsRouted(accessCase, AccessCategoryStage.Fulfiller, actorUserId);
        bool isRoutedCloser = IsRouted(accessCase, AccessCategoryStage.Closer, actorUserId);
        bool isRoutedFallbackVerifier = (accessCase.RouteParticipants ?? [])
            .Any(r => StageEquals(r.Stage, AccessCategoryStage.Verifier)
                && !r.IsSubjectEmployeeDerived
                && r.UserId == actorUserId);

        bool canApprove = isApproval && hasAccessApprove && isRoutedApprover && !isRequester;
        bool canReject = canApprove;
        bool canSendForRework = canApprove;
        bool canFulfill = isFulfillment && hasAccessFulfill && isRoutedFulfiller;
        bool canSendForVerification = canFulfill;
        bool canVerifyAsEmployee = isVerification && !accessCase.IsReadyToClose && isSubject && !isLeaver;
        bool canVerifyAsFallback = isVerification && !accessCase.IsReadyToClose && isRoutedFallbackVerifier;
        bool canClose = isVerification && accessCase.IsReadyToClose && hasAccessFulfill && isRoutedCloser;
        bool canSubmit = isDraft && (hasAccessRequest || isRequester);
        bool canResubmit = isRework && (hasAccessRequest || isRequester);
        bool canEditRequest = (isDraft || isRework) && (hasAccessRequest || isRequester || hasAccessConfigure);
        bool isRoutedApproverMissingPermission = isApproval && isSnapshottedApprover && !hasAccessApprove;

        return Task.FromResult(new AccessCaseActionsDto(
            canSubmit,
            canResubmit,
            canEditRequest,
            canApprove,
            canReject,
            canSendForRework,
            canFulfill,
            canSendForVerification,
            canVerifyAsEmployee,
            canVerifyAsFallback,
            canClose,
            isRoutedApproverMissingPermission));
    }

    private static bool IsRouted(AccessCaseDto accessCase, AccessCategoryStage stage, Guid userId)
    {
        IReadOnlyList<AccessCaseRouteParticipantDto> routes =
            accessCase.RouteParticipants ?? Array.Empty<AccessCaseRouteParticipantDto>();
        AccessCaseRouteParticipantDto[] stageRoutes = routes
            .Where(r => StageEquals(r.Stage, stage))
            .ToArray();

        if (stageRoutes.Length > 0)
            return stageRoutes.Any(r => r.UserId == userId);

        // Empty stage with other route rows → invalid config (do not open to everyone).
        if (routes.Count > 0)
            return false;

        // Legacy cases with zero total route participants.
        if (stage == AccessCategoryStage.Approver)
            return accessCase.DesignatedApproverUserId == userId;

        // Legacy fulfillment/verification/closure relied on permission only.
        return stage is AccessCategoryStage.Fulfiller
            or AccessCategoryStage.Closer
            or AccessCategoryStage.Verifier;
    }

    private static bool StatusEquals(string status, AccessCaseStatus expected) =>
        string.Equals(status, expected.ToString(), StringComparison.OrdinalIgnoreCase);

    private static bool StageEquals(string stage, AccessCategoryStage expected) =>
        string.Equals(stage, expected.ToString(), StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<Guid>> GetRouteUserIdsAsync(
        Guid caseId, AccessCategoryStage stage, CancellationToken ct)
    {
        return await db.AccessCaseRouteParticipants.AsNoTracking()
            .Where(x => x.AccessCaseId == caseId && x.Stage == stage)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(ct);
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
        Dictionary<Guid, string> categoryNames = await ResolveLiveCategoryNamesAsync(items, ct);
        return items.Select(x => Map(x, counts.GetValueOrDefault(x.Id), pending.GetValueOrDefault(x.Id),
            liveCategoryNames: categoryNames)).ToList();
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
        IReadOnlyList<AccessCaseItemCreateSpec>? items = null,
        bool submitForApproval = false)
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

            _ = await AddCaseItemsOnCreateAsync(
                entity, type, accessCategoryId, items, innerCt);

            if (type == AccessCaseType.Mover && subjectUserId is Guid moverSubject && moverSubject != Guid.Empty)
            {
                await SnapshotCurrentAccessAsync(entity.Id, moverSubject, innerCt);
                int snapshotCount = await db.ExistingAccessSnapshotItems.CountAsync(
                    x => x.AccessCaseId == entity.Id, innerCt);
                if (snapshotCount > 0)
                    entity.ConfirmExistingAccess(requesterUserId, clock.UtcNow);
            }

            await businessAudit.AppendAsync(AccessAudit.Created(entity.Id, entity.CaseNumber), innerCt);
            await db.SaveChangesAsync(innerCt);

            if (submitForApproval)
                await SubmitCoreAsync(entity, requesterUserId, innerCt);

            created = (await GetAsync(entity.Id, innerCt))!;
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
                if (action is not (AccessItemAction.Grant or AccessItemAction.Remove
                    or AccessItemAction.Disable or AccessItemAction.Reassign))
                    throw new InvalidOperationException("Mover cases support Grant, Remove, Disable, or Reassign actions.");
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
        await SubmitCoreAsync(entity, actorUserId, ct);
        return (await GetAsync(id, ct))!;
    }

    private async Task SubmitCoreAsync(AccessCase entity, Guid actorUserId, CancellationToken ct)
    {
        if (entity.Status != AccessCaseStatus.Draft)
            throw new InvalidOperationException("Only draft cases can be submitted.");
        if (entity.AccessCategoryId is null)
            throw new InvalidOperationException("Access category is required before submission.");

        AccessCategory category = await db.AccessCategories.AsNoTracking()
            .FirstAsync(x => x.Id == entity.AccessCategoryId, ct);
        if (!category.IsActive)
            throw new InvalidOperationException($"{category.NameEn} is inactive.");

        bool allowedRequester = await db.AccessCategoryParticipants.AnyAsync(
            x => x.AccessCategoryId == category.Id
                && x.Stage == AccessCategoryStage.Requester
                && x.UserId == actorUserId, ct);
        if (!allowedRequester && actorUserId != entity.RequesterUserId)
            throw new InvalidOperationException("Only the requester can submit this case.");
        if (!allowedRequester)
            throw new InvalidOperationException("You are not allowed to request this access category.");

        await ValidateItemsForSubmitAsync(entity, ct);

        List<AccessCategoryParticipant> configured = await db.AccessCategoryParticipants
            .Where(x => x.AccessCategoryId == category.Id).ToListAsync(ct);

        bool hasApprover = configured.Any(x => x.Stage == AccessCategoryStage.Approver);
        if (!hasApprover)
            throw new InvalidOperationException($"{category.NameEn} has no approver configured.");

        bool hasCloser = configured.Any(x => x.Stage == AccessCategoryStage.Closer);
        if (!hasCloser)
            throw new InvalidOperationException($"{category.NameEn} has no closer configured.");

        List<AccessCaseItem> pendingItems = await db.AccessCaseItems.AsNoTracking()
            .Where(x => x.AccessCaseId == entity.Id && x.Status == AccessItemStatus.Pending)
            .ToListAsync(ct);
        bool hasWorkItems = pendingItems.Any(x =>
            x.Action is AccessItemAction.Grant or AccessItemAction.Remove
                or AccessItemAction.Disable or AccessItemAction.Reassign);
        if (hasWorkItems && !configured.Any(x => x.Stage == AccessCategoryStage.Fulfiller))
            throw new InvalidOperationException($"{category.NameEn} has no fulfiller configured.");

        bool canUseSubjectVerifier = category.PreferSubjectEmployeeVerification
            && entity.Type != AccessCaseType.Leaver
            && entity.SubjectUserId is Guid subjectForVerify
            && subjectForVerify != Guid.Empty;
        bool hasConfiguredVerifier = configured.Any(x => x.Stage == AccessCategoryStage.Verifier);
        if (!canUseSubjectVerifier && !hasConfiguredVerifier)
        {
            if (entity.Type == AccessCaseType.Leaver)
                throw new InvalidOperationException($"{category.NameEn} has no verifier configured (required for leaver cases).");
            if (entity.SubjectUserId is null)
                throw new InvalidOperationException($"{category.NameEn} has no verifier configured (required for external subjects).");
            throw new InvalidOperationException($"{category.NameEn} has no verifier configured.");
        }

        // Snapshot routing for durability
        List<AccessCaseRouteParticipant> old = await db.AccessCaseRouteParticipants
            .Where(x => x.AccessCaseId == entity.Id).ToListAsync(ct);
        db.AccessCaseRouteParticipants.RemoveRange(old);

        foreach (AccessCategoryParticipant part in configured)
        {
            db.AccessCaseRouteParticipants.Add(AccessCaseRouteParticipant.Create(
                entity.Id, part.Stage, part.UserId, clock.UtcNow));
        }

        // Preferred subject employee verifier (not for Leaver)
        if (canUseSubjectVerifier && entity.SubjectUserId is Guid subjectId)
        {
            bool already = configured.Any(x =>
                x.Stage == AccessCategoryStage.Verifier && x.UserId == subjectId);
            if (!already)
            {
                db.AccessCaseRouteParticipants.Add(AccessCaseRouteParticipant.Create(
                    entity.Id, AccessCategoryStage.Verifier, subjectId, clock.UtcNow,
                    isSubjectEmployeeDerived: true));
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

        await CreatePendingRevisionSnapshotAsync(entity, actorUserId, revisionNumber: 1, ct);

        await db.SaveChangesAsync(ct);
    }

    private async Task CreatePendingRevisionSnapshotAsync(
        AccessCase entity,
        Guid submittedByUserId,
        int revisionNumber,
        CancellationToken ct)
    {
        AccessCaseRevision revision = AccessCaseRevision.CreatePending(
            entity.Id, revisionNumber, submittedByUserId, clock.UtcNow);
        db.AccessCaseRevisions.Add(revision);

        List<AccessCaseItem> items = await db.AccessCaseItems
            .Where(x => x.AccessCaseId == entity.Id)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);
        foreach (AccessCaseItem item in items)
            db.AccessCaseRevisionItems.Add(AccessCaseRevisionItem.CreateFromCaseItem(revision.Id, item));

        entity.SetCurrentScopeRevisionNumber(revisionNumber, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "ScopeRevisionCreated", null, revisionNumber.ToString()), ct);
    }

    private async Task<AccessCaseRevision> RequireCurrentPendingRevisionAsync(AccessCase entity, CancellationToken ct)
    {
        AccessCaseRevision? revision = await db.AccessCaseRevisions
            .FirstOrDefaultAsync(
                x => x.AccessCaseId == entity.Id
                    && x.RevisionNumber == entity.CurrentScopeRevisionNumber
                    && x.Decision == AccessCaseRevisionDecision.Pending,
                ct);
        if (revision is null)
            throw new InvalidOperationException("No pending scope revision found for this case.");
        return revision;
    }

    private async Task ValidateItemsForSubmitAsync(AccessCase entity, CancellationToken ct)
    {
        List<AccessCaseItem> items = await db.AccessCaseItems.AsNoTracking()
            .Where(x => x.AccessCaseId == entity.Id)
            .ToListAsync(ct);

        switch (entity.Type)
        {
            case AccessCaseType.Joiner:
            case AccessCaseType.AccessRequest:
                if (!items.Any(x => x.Action == AccessItemAction.Grant))
                    throw new InvalidOperationException($"{entity.Type} cases require at least one Grant item.");
                break;
            case AccessCaseType.Mover:
                if (!items.Any(x => x.Action is AccessItemAction.Grant or AccessItemAction.Remove
                        or AccessItemAction.Disable or AccessItemAction.Reassign))
                    throw new InvalidOperationException(
                        "Mover cases require at least one Grant, Remove, Disable, or Reassign item (not keep-only).");
                break;
            case AccessCaseType.Leaver:
                if (!items.Any(x => x.Action is AccessItemAction.Remove or AccessItemAction.Disable
                        or AccessItemAction.Reassign))
                    throw new InvalidOperationException(
                        "Leaver cases require at least one Remove, Disable, or Reassign item.");
                break;
        }
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

        if (entity.CurrentScopeRevisionNumber > 0)
        {
            AccessCaseRevision revision = await RequireCurrentPendingRevisionAsync(entity, ct);
            revision.RecordDecision(AccessCaseRevisionDecision.Approved, actorUserId, clock.UtcNow);
        }

        entity.RecordApproval(actorUserId, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "CaseApproved", null, actorUserId.ToString()), ct);
        return await TransitionAsync(id, AccessCaseStatus.Fulfillment, ct, actorUserId);
    }

    public async Task<AccessCaseDto> ReturnForReworkAsync(Guid id, Guid actorUserId, string reason, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        AccessCase entity = await LoadTrackedAsync(id, ct);
        if (entity.Status != AccessCaseStatus.Approval)
            throw new InvalidOperationException("Case is not awaiting approval.");
        if (actorUserId == entity.RequesterUserId)
            throw new InvalidOperationException("Requester cannot send their own access case for rework.");
        await EnsureRouteActorAsync(entity.Id, AccessCategoryStage.Approver, actorUserId, ct,
            legacyDesignatedApprover: entity.DesignatedApproverUserId);

        string trimmed = reason.Trim();
        if (entity.CurrentScopeRevisionNumber > 0)
        {
            AccessCaseRevision revision = await RequireCurrentPendingRevisionAsync(entity, ct);
            revision.RecordDecision(AccessCaseRevisionDecision.Rework, actorUserId, clock.UtcNow, trimmed);
        }

        entity.RecordRework(actorUserId, trimmed, clock.UtcNow);
        AccessCaseStatus from = entity.Status;
        entity.TransitionTo(AccessCaseStatus.Rework, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "CaseReturnedForRework", from.ToString(), nameof(AccessCaseStatus.Rework),
            BusinessAuditAction.StatusChanged, trimmed), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<AccessCaseDto> ResubmitAsync(Guid id, Guid actorUserId, CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        if (entity.Status != AccessCaseStatus.Rework)
            throw new InvalidOperationException("Only cases in Rework can be resubmitted.");
        if (actorUserId != entity.RequesterUserId)
        {
            bool allowedRequester = entity.AccessCategoryId is Guid categoryId
                && await db.AccessCategoryParticipants.AnyAsync(
                    x => x.AccessCategoryId == categoryId
                        && x.Stage == AccessCategoryStage.Requester
                        && x.UserId == actorUserId, ct);
            if (!allowedRequester)
                throw new InvalidOperationException("Only the requester can resubmit this case.");
        }

        await ValidateItemsForSubmitAsync(entity, ct);

        int nextRevision = entity.CurrentScopeRevisionNumber <= 0
            ? 1
            : entity.CurrentScopeRevisionNumber + 1;
        await CreatePendingRevisionSnapshotAsync(entity, actorUserId, nextRevision, ct);

        entity.ClearReworkOnResubmit(clock.UtcNow);
        AccessCaseStatus from = entity.Status;
        entity.TransitionTo(AccessCaseStatus.Approval, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "CaseResubmitted", from.ToString(), nameof(AccessCaseStatus.Approval),
            BusinessAuditAction.StatusChanged), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<AccessCaseDto> UpdateRequestScopeAsync(
        Guid id,
        string? reason,
        IReadOnlyList<AccessCaseItemCreateSpec> items,
        Guid actorUserId,
        CancellationToken ct)
    {
        AccessCase entity = await LoadTrackedAsync(id, ct);
        if (entity.Status is not (AccessCaseStatus.Draft or AccessCaseStatus.Rework))
            throw new InvalidOperationException(
                "Requested access can only be changed while the request is Draft or in Rework.");

        if (!string.IsNullOrWhiteSpace(reason))
        {
            if (entity.Status == AccessCaseStatus.Draft)
            {
                entity.UpdateDraft(
                    reason,
                    entity.SubjectUserId,
                    entity.SubjectName,
                    entity.SubjectEmail,
                    entity.DepartmentId,
                    entity.ManagerUserId,
                    entity.DesignatedApproverUserId,
                    entity.EffectiveAtUtc,
                    clock.UtcNow,
                    entity.AccessCategoryId);
            }
            else
            {
                entity.UpdateReasonWhileEditable(reason, clock.UtcNow);
            }
        }

        List<AccessCaseItem> existing = await db.AccessCaseItems
            .Where(x => x.AccessCaseId == id).ToListAsync(ct);
        db.AccessCaseItems.RemoveRange(existing);

        List<AccessCaseItemCreateSpec> selected = items.Where(x => x.IsSelected).ToList();
        if (selected.Count > 0)
            await MaterializeClientItemsAsync(entity, entity.Type, selected, ct);

        if (entity.Status == AccessCaseStatus.Rework)
        {
            await businessAudit.AppendAsync(AccessAudit.Field(
                entity.Id, entity.CaseNumber, "RequestEditedAfterRework", null, actorUserId.ToString()), ct);
        }
        else
        {
            await businessAudit.AppendAsync(AccessAudit.Field(
                entity.Id, entity.CaseNumber, "RequestScopeUpdated", null, actorUserId.ToString()), ct);
        }

        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<IReadOnlyList<AccessCaseRevisionDto>> ListRevisionsAsync(Guid caseId, CancellationToken ct)
    {
        List<AccessCaseRevision> revisions = await db.AccessCaseRevisions.AsNoTracking()
            .Where(x => x.AccessCaseId == caseId)
            .OrderBy(x => x.RevisionNumber)
            .ToListAsync(ct);
        if (revisions.Count == 0) return [];

        List<Guid> revisionIds = revisions.Select(x => x.Id).ToList();
        List<AccessCaseRevisionItem> items = await db.AccessCaseRevisionItems.AsNoTracking()
            .Where(x => revisionIds.Contains(x.RevisionId))
            .ToListAsync(ct);
        Dictionary<Guid, List<AccessCaseRevisionItemDto>> byRevision = items
            .GroupBy(x => x.RevisionId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(MapRevisionItem).ToList());

        return revisions.Select(r => new AccessCaseRevisionDto(
            r.Id,
            r.AccessCaseId,
            r.RevisionNumber,
            r.SubmittedByUserId,
            r.SubmittedAtUtc,
            r.Decision.ToString(),
            r.DecidedByUserId,
            r.DecidedAtUtc,
            r.DecisionReason,
            byRevision.GetValueOrDefault(r.Id) ?? [])).ToList();
    }

    public async Task<AccessCaseDto> RejectAsync(Guid id, Guid actorUserId, string? reason, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        AccessCase entity = await LoadTrackedAsync(id, ct);
        if (entity.Status != AccessCaseStatus.Approval)
            throw new InvalidOperationException("Case is not awaiting approval.");
        if (actorUserId == entity.RequesterUserId)
            throw new InvalidOperationException("Requester cannot reject their own access case.");
        await EnsureRouteActorAsync(entity.Id, AccessCategoryStage.Approver, actorUserId, ct,
            legacyDesignatedApprover: entity.DesignatedApproverUserId);

        string trimmed = reason.Trim();
        if (entity.CurrentScopeRevisionNumber > 0)
        {
            AccessCaseRevision revision = await RequireCurrentPendingRevisionAsync(entity, ct);
            revision.RecordDecision(AccessCaseRevisionDecision.Rejected, actorUserId, clock.UtcNow, trimmed);
        }

        entity.RecordRejection(actorUserId, trimmed, clock.UtcNow);
        AccessCaseStatus from = entity.Status;
        entity.TransitionTo(AccessCaseStatus.Rejected, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.CaseNumber, "CaseRejected", from.ToString(), nameof(AccessCaseStatus.Rejected),
            BusinessAuditAction.StatusChanged, trimmed), ct);
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
        if (entity.Status != AccessCaseStatus.Draft)
            throw new InvalidOperationException("Requested access can only be changed while the case is in Draft.");
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
        EnsureEditableScope(entity);
        AccessCaseItem item = AccessCaseItem.Create(
            caseId, entitlementKey, action, clock.UtcNow, configurationItemId, isPrivileged, isMandatory, notes);
        db.AccessCaseItems.Add(item);
        if (entity.Status == AccessCaseStatus.Rework)
        {
            await businessAudit.AppendAsync(AccessAudit.Field(
                entity.Id, entity.CaseNumber, "RequestEditedAfterRework", null, entitlementKey), ct);
        }
        else
        {
            await businessAudit.AppendAsync(AccessAudit.Field(
                entity.Id, entity.CaseNumber, "ItemAdded", null, entitlementKey), ct);
        }
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
        EnsureEditableScope(entity);
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

    private static void EnsureEditableScope(AccessCase entity)
    {
        if (entity.Status is not (AccessCaseStatus.Draft or AccessCaseStatus.Rework))
            throw new InvalidOperationException(
                "Requested access can only be changed while the request is Draft or in Rework.");
    }

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

        // Legacy allow-anyone only when the case has ZERO route participants (pre-routing cases).
        // If the case has any route snapshot rows, stage access is restricted to listed actors —
        // an empty stage list does NOT open the stage to everyone.
        bool hasAnyRouteParticipants = await db.AccessCaseRouteParticipants.AsNoTracking()
            .AnyAsync(x => x.AccessCaseId == caseId, ct);
        if (!hasAnyRouteParticipants)
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

    private async Task<Dictionary<Guid, string>> ResolveLiveCategoryNamesAsync(
        IReadOnlyList<AccessCase> cases, CancellationToken ct)
    {
        List<Guid> ids = cases
            .Where(x => string.IsNullOrWhiteSpace(x.AccessCategoryNameSnapshot) && x.AccessCategoryId is Guid)
            .Select(x => x.AccessCategoryId!.Value)
            .Distinct()
            .ToList();
        if (ids.Count == 0) return [];

        return await db.AccessCategories.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NameEn, ct);
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
        IReadOnlyList<AccessCaseRouteParticipantDto>? routes = null,
        IReadOnlyDictionary<Guid, string>? liveCategoryNames = null)
    {
        string? displayName = x.AccessCategoryNameSnapshot;
        if (string.IsNullOrWhiteSpace(displayName)
            && x.AccessCategoryId is Guid categoryId
            && liveCategoryNames is not null
            && liveCategoryNames.TryGetValue(categoryId, out string? liveName))
        {
            displayName = liveName;
        }

        return new(x.Id, x.CaseNumber, x.Type.ToString(), x.Status.ToString(), x.RequesterUserId,
            x.SubjectUserId, x.SubjectName, x.SubjectEmail, x.DepartmentId, x.ManagerUserId,
            x.DesignatedApproverUserId, x.LinkedTicketId, x.VendorId, x.EffectiveAtUtc, x.Reason,
            x.ExistingAccessConfirmed, x.ExistingAccessConfirmedAtUtc, x.ExistingAccessConfirmedByUserId,
            x.CreatedAtUtc, x.UpdatedAtUtc, x.ClosedAtUtc, Convert.ToBase64String(x.RowVersion),
            itemCount, pendingMandatory,
            x.AccessCategoryId, x.AccessCategoryKeySnapshot,
            // Prefer stored snapshot; for drafts without snapshot, surface live category name in both fields.
            x.AccessCategoryNameSnapshot ?? displayName,
            x.PreferSubjectEmployeeVerificationSnapshot,
            x.ApprovedByUserId, x.ApprovedAtUtc,
            x.VerifiedByUserId, x.VerifiedAtUtc,
            x.VerificationMethod?.ToString(), x.VerificationOutcome?.ToString(),
            x.VerificationComment, x.FallbackReason, x.ClosedByUserId,
            x.IsReadyToClose, routes, displayName,
            Actions: null,
            ReturnedForReworkByUserId: x.ReturnedForReworkByUserId,
            ReturnedForReworkAtUtc: x.ReturnedForReworkAtUtc,
            ReworkReason: x.ReworkReason,
            RejectedByUserId: x.RejectedByUserId,
            RejectedAtUtc: x.RejectedAtUtc,
            RejectionReason: x.RejectionReason,
            CurrentScopeRevisionNumber: x.CurrentScopeRevisionNumber);
    }

    private static AccessCaseItemDto Map(AccessCaseItem x) =>
        new(x.Id, x.AccessCaseId, x.ConfigurationItemId, x.EntitlementKey, x.Action.ToString(),
            x.IsPrivileged, x.IsMandatory, x.Status.ToString(), x.FulfilledByUserId, x.FulfilledAtUtc,
            x.Notes, x.CreatedAtUtc,
            x.AccessEntitlementId,
            x.EntitlementNameEnSnapshot ?? x.EntitlementKey,
            x.EntitlementNameArSnapshot ?? x.EntitlementNameEnSnapshot ?? x.EntitlementKey,
            x.IsCustom);

    private static AccessCaseRevisionItemDto MapRevisionItem(AccessCaseRevisionItem x) =>
        new(x.Id, x.RevisionId, x.AccessEntitlementId, x.EntitlementKeySnapshot,
            x.NameEnSnapshot, x.NameArSnapshot, x.CustomName, x.Action.ToString(),
            x.Notes, x.IsPrivileged, x.IsCustom);
}
