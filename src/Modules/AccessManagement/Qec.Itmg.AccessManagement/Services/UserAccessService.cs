using Microsoft.EntityFrameworkCore;
using Qec.Itmg.AccessManagement.Domain;
using Qec.Itmg.AccessManagement.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;

namespace Qec.Itmg.AccessManagement.Services;

public sealed record UserAccessEntitlementDto(
    Guid Id,
    Guid UserId,
    Guid? AccessEntitlementId,
    string EntitlementKey,
    string NameEn,
    string NameAr,
    string Status,
    string? DefaultRevokeAction,
    bool IsPrivileged,
    bool IsCustom,
    Guid? GrantedFromAccessCaseId,
    Guid? LastChangedFromAccessCaseId,
    DateTimeOffset? GrantedAtUtc,
    DateTimeOffset? RevokedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed class UserAccessService(
    AccessManagementDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit)
{
    public async Task<IReadOnlyList<UserAccessEntitlementDto>> GetCurrentAccessAsync(
        Guid userId,
        CancellationToken ct,
        bool activeOnly = true)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));

        IQueryable<UserAccessEntitlement> q = db.UserAccessEntitlements.AsNoTracking()
            .Where(x => x.UserId == userId);
        if (activeOnly) q = q.Where(x => x.Status == UserAccessEntitlementStatus.Active);

        List<UserAccessEntitlement> rows = await q.OrderBy(x => x.EntitlementNameSnapshot).ToListAsync(ct);
        List<Guid> entitlementIds = rows
            .Where(x => x.AccessEntitlementId is not null)
            .Select(x => x.AccessEntitlementId!.Value)
            .Distinct()
            .ToList();
        Dictionary<Guid, AccessEntitlement> catalog = entitlementIds.Count == 0
            ? []
            : await db.AccessEntitlements.AsNoTracking()
                .Where(x => entitlementIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

        return rows.Select(x => Map(x, catalog)).ToList();
    }

    public async Task ReconcileFromClosedCaseAsync(AccessCase accessCase, CancellationToken ct)
    {
        if (accessCase.Status != AccessCaseStatus.Closed)
            throw new InvalidOperationException("Only closed cases can reconcile current access.");
        if (accessCase.SubjectUserId is not Guid subjectUserId || subjectUserId == Guid.Empty)
            return;

        List<AccessCaseItem> items = await db.AccessCaseItems
            .Where(x => x.AccessCaseId == accessCase.Id && x.Status == AccessItemStatus.Completed)
            .ToListAsync(ct);
        if (items.Count == 0) return;

        List<UserAccessEntitlement> existing = await db.UserAccessEntitlements
            .Where(x => x.UserId == subjectUserId)
            .ToListAsync(ct);

        foreach (AccessCaseItem item in items)
        {
            if (item.Action == AccessItemAction.Reassign)
                continue;

            UserAccessEntitlement? row = FindExisting(existing, item);
            string name = item.EntitlementNameEnSnapshot
                ?? item.EntitlementNameArSnapshot
                ?? item.EntitlementKey;

            switch (item.Action)
            {
                case AccessItemAction.Grant:
                    if (row is null)
                    {
                        row = UserAccessEntitlement.CreateActive(
                            subjectUserId,
                            item.EntitlementKey,
                            name,
                            clock.UtcNow,
                            item.AccessEntitlementId,
                            accessCase.Id);
                        db.UserAccessEntitlements.Add(row);
                        existing.Add(row);
                    }
                    else
                    {
                        row.ApplyGrant(accessCase.Id, clock.UtcNow, name);
                    }

                    await businessAudit.AppendAsync(AccessAudit.Field(
                        accessCase.Id, accessCase.CaseNumber, "CurrentAccessGranted",
                        null, item.EntitlementKey), ct);
                    break;

                case AccessItemAction.Remove:
                    if (row is null)
                    {
                        row = UserAccessEntitlement.CreateActive(
                            subjectUserId,
                            item.EntitlementKey,
                            name,
                            clock.UtcNow,
                            item.AccessEntitlementId,
                            accessCase.Id);
                        db.UserAccessEntitlements.Add(row);
                        existing.Add(row);
                    }

                    row.ApplyRemove(accessCase.Id, clock.UtcNow);
                    await businessAudit.AppendAsync(AccessAudit.Field(
                        accessCase.Id, accessCase.CaseNumber, "CurrentAccessRemoved",
                        null, item.EntitlementKey), ct);
                    break;

                case AccessItemAction.Disable:
                    if (row is null)
                    {
                        row = UserAccessEntitlement.CreateActive(
                            subjectUserId,
                            item.EntitlementKey,
                            name,
                            clock.UtcNow,
                            item.AccessEntitlementId,
                            accessCase.Id);
                        db.UserAccessEntitlements.Add(row);
                        existing.Add(row);
                    }

                    row.ApplyDisable(accessCase.Id, clock.UtcNow);
                    await businessAudit.AppendAsync(AccessAudit.Field(
                        accessCase.Id, accessCase.CaseNumber, "CurrentAccessDisabled",
                        null, item.EntitlementKey), ct);
                    break;
            }
        }
    }

    public async Task EnsureActiveAsync(
        Guid userId,
        Guid accessEntitlementId,
        string key,
        string name,
        CancellationToken ct)
    {
        bool exists = await db.UserAccessEntitlements.AnyAsync(
            x => x.UserId == userId
                && x.AccessEntitlementId == accessEntitlementId
                && x.Status == UserAccessEntitlementStatus.Active, ct);
        if (exists) return;

        db.UserAccessEntitlements.Add(UserAccessEntitlement.CreateActive(
            userId, key, name, clock.UtcNow, accessEntitlementId));
    }

    private static UserAccessEntitlement? FindExisting(
        List<UserAccessEntitlement> existing,
        AccessCaseItem item)
    {
        if (item.AccessEntitlementId is Guid entitlementId)
        {
            UserAccessEntitlement? byId = existing.FirstOrDefault(x => x.AccessEntitlementId == entitlementId);
            if (byId is not null) return byId;
        }

        return existing.FirstOrDefault(x =>
            x.AccessEntitlementId is null
            && string.Equals(x.EntitlementKeySnapshot, item.EntitlementKey, StringComparison.OrdinalIgnoreCase));
    }

    private static UserAccessEntitlementDto Map(
        UserAccessEntitlement x,
        IReadOnlyDictionary<Guid, AccessEntitlement> catalog)
    {
        AccessEntitlement? ent = x.AccessEntitlementId is Guid id && catalog.TryGetValue(id, out AccessEntitlement? found)
            ? found
            : null;
        return new(
            x.Id,
            x.UserId,
            x.AccessEntitlementId,
            x.EntitlementKeySnapshot,
            ent?.NameEn ?? x.EntitlementNameSnapshot,
            ent?.NameAr ?? x.EntitlementNameSnapshot,
            x.Status.ToString(),
            ent?.DefaultRevokeAction.ToString(),
            ent?.IsPrivileged ?? false,
            x.AccessEntitlementId is null,
            x.GrantedFromAccessCaseId,
            x.LastChangedFromAccessCaseId,
            x.GrantedAtUtc,
            x.RevokedAtUtc,
            x.UpdatedAtUtc);
    }
}
