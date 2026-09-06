using Microsoft.EntityFrameworkCore;
using Qec.Itmg.AccessManagement.Domain;
using Qec.Itmg.AccessManagement.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Identity;

namespace Qec.Itmg.AccessManagement.Services;

public sealed record AccessCategoryDto(
    Guid Id,
    string Key,
    string NameEn,
    string NameAr,
    string? DescriptionEn,
    string? DescriptionAr,
    bool IsActive,
    bool PreferSubjectEmployeeVerification,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string RowVersion,
    IReadOnlyDictionary<string, IReadOnlyList<Guid>> ParticipantsByStage);

public sealed record AccessCategoryParticipantUserDto(
    Guid UserId,
    string? DisplayName,
    string? Upn);

public sealed record AccessCategoryEntitlementDto(
    Guid Id,
    Guid AccessCategoryId,
    Guid AccessEntitlementId,
    string EntitlementKey,
    string NameEn,
    string NameAr,
    string DefaultRevokeAction,
    bool IsPrivileged,
    bool IsDefaultForJoiner,
    int SortOrder,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AccessCategoryEntitlementSpec(
    Guid AccessEntitlementId,
    bool IsDefaultForJoiner,
    int SortOrder,
    bool IsActive);

public sealed class AccessCategoryService(
    AccessManagementDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    IActiveEmployeeLookup employees)
{
    public async Task<IReadOnlyList<AccessCategoryDto>> ListAsync(bool activeOnly, CancellationToken ct)
    {
        IQueryable<AccessCategory> q = db.AccessCategories.AsNoTracking();
        if (activeOnly) q = q.Where(x => x.IsActive);
        List<AccessCategory> items = await q.OrderBy(x => x.Key).ToListAsync(ct);
        List<AccessCategoryDto> result = [];
        foreach (AccessCategory item in items)
            result.Add(await MapAsync(item, ct));
        return result;
    }

    public async Task<AccessCategoryDto?> GetAsync(Guid id, CancellationToken ct)
    {
        AccessCategory? item = await db.AccessCategories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : await MapAsync(item, ct);
    }

    public async Task<AccessCategoryDto> CreateAsync(
        string key,
        string nameEn,
        string nameAr,
        string? descriptionEn,
        string? descriptionAr,
        bool preferSubjectEmployeeVerification,
        bool isActive,
        CancellationToken ct)
    {
        string normalizedKey = key.Trim().ToUpperInvariant();
        bool exists = await db.AccessCategories.AnyAsync(x => x.Key == normalizedKey, ct);
        if (exists) throw new InvalidOperationException($"Category key '{normalizedKey}' already exists.");

        AccessCategory entity = AccessCategory.Create(
            normalizedKey, nameEn, nameAr, clock.UtcNow, descriptionEn, descriptionAr,
            preferSubjectEmployeeVerification, isActive);
        db.AccessCategories.Add(entity);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.Key, "AccessCategoryCreated", null, entity.NameEn, BusinessAuditAction.Created), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<AccessCategoryDto> UpdateAsync(
        Guid id,
        string nameEn,
        string nameAr,
        string? descriptionEn,
        string? descriptionAr,
        bool isActive,
        bool preferSubjectEmployeeVerification,
        CancellationToken ct)
    {
        AccessCategory entity = await db.AccessCategories.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Access category not found.");
        entity.Update(nameEn, nameAr, descriptionEn, descriptionAr, isActive, preferSubjectEmployeeVerification, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.Key, "AccessCategoryUpdated", null, entity.NameEn), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<AccessCategoryDto> ReplaceRoutingAsync(
        Guid id,
        IReadOnlyDictionary<AccessCategoryStage, IReadOnlyList<Guid>> routing,
        CancellationToken ct)
    {
        AccessCategory entity = await db.AccessCategories.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Access category not found.");

        HashSet<Guid> activeIds = (await employees.ListActiveAsync(ct)).Select(x => x.Id).ToHashSet();
        List<AccessCategoryParticipant> existing = await db.AccessCategoryParticipants
            .Where(x => x.AccessCategoryId == id).ToListAsync(ct);
        db.AccessCategoryParticipants.RemoveRange(existing);

        foreach ((AccessCategoryStage stage, IReadOnlyList<Guid> userIds) in routing)
        {
            foreach (Guid userId in userIds.Distinct())
            {
                if (!activeIds.Contains(userId))
                    throw new InvalidOperationException("Only active users can be assigned to category routing.");
                db.AccessCategoryParticipants.Add(
                    AccessCategoryParticipant.Create(id, stage, userId, clock.UtcNow));
            }
        }

        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.Key, "CategoryRoutingChanged", null,
            string.Join(",", routing.Select(kv => $"{kv.Key}:{kv.Value.Count}"))), ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<IReadOnlyList<AccessCategoryEntitlementDto>> ListEntitlementsAsync(
        Guid categoryId,
        CancellationToken ct,
        bool activeOnly = false)
    {
        _ = await db.AccessCategories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == categoryId, ct)
            ?? throw new InvalidOperationException("Access category not found.");

        IQueryable<AccessCategoryEntitlement> q = db.AccessCategoryEntitlements.AsNoTracking()
            .Where(x => x.AccessCategoryId == categoryId);
        if (activeOnly) q = q.Where(x => x.IsActive);

        List<AccessCategoryEntitlement> mappings = await q.OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAtUtc).ToListAsync(ct);
        List<Guid> entitlementIds = mappings.Select(x => x.AccessEntitlementId).Distinct().ToList();
        Dictionary<Guid, AccessEntitlement> entitlements = entitlementIds.Count == 0
            ? []
            : await db.AccessEntitlements.AsNoTracking()
                .Where(x => entitlementIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

        return mappings.Select(m =>
        {
            AccessEntitlement ent = entitlements.GetValueOrDefault(m.AccessEntitlementId)
                ?? throw new InvalidOperationException("Mapped entitlement is missing.");
            return MapCategoryEntitlement(m, ent);
        }).ToList();
    }

    public async Task<IReadOnlyList<AccessCategoryEntitlementDto>> ReplaceEntitlementsAsync(
        Guid categoryId,
        IReadOnlyList<AccessCategoryEntitlementSpec> specs,
        CancellationToken ct)
    {
        AccessCategory category = await db.AccessCategories.FirstOrDefaultAsync(x => x.Id == categoryId, ct)
            ?? throw new InvalidOperationException("Access category not found.");

        List<Guid> requestedIds = specs.Select(x => x.AccessEntitlementId).Distinct().ToList();
        if (requestedIds.Count != specs.Count)
            throw new InvalidOperationException("Duplicate entitlement mappings are not allowed.");

        Dictionary<Guid, AccessEntitlement> catalog = requestedIds.Count == 0
            ? []
            : await db.AccessEntitlements.Where(x => requestedIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        foreach (Guid id in requestedIds)
        {
            if (!catalog.ContainsKey(id))
                throw new InvalidOperationException("Access entitlement not found.");
        }

        List<AccessCategoryEntitlement> existing = await db.AccessCategoryEntitlements
            .Where(x => x.AccessCategoryId == categoryId).ToListAsync(ct);
        Dictionary<Guid, AccessCategoryEntitlement> byEntitlement = existing
            .ToDictionary(x => x.AccessEntitlementId);
        List<Guid> existingEntitlementIds = existing.Select(x => x.AccessEntitlementId).Distinct().ToList();
        Dictionary<Guid, string> existingKeys = existingEntitlementIds.Count == 0
            ? []
            : await db.AccessEntitlements.AsNoTracking()
                .Where(x => existingEntitlementIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Key, ct);

        HashSet<Guid> keep = [];
        foreach (AccessCategoryEntitlementSpec spec in specs)
        {
            keep.Add(spec.AccessEntitlementId);
            if (byEntitlement.TryGetValue(spec.AccessEntitlementId, out AccessCategoryEntitlement? row))
            {
                bool changed = row.IsDefaultForJoiner != spec.IsDefaultForJoiner
                    || row.SortOrder != spec.SortOrder
                    || row.IsActive != spec.IsActive;
                if (changed)
                {
                    row.Update(spec.IsDefaultForJoiner, spec.SortOrder, spec.IsActive, clock.UtcNow);
                    await businessAudit.AppendAsync(AccessAudit.Field(
                        category.Id, category.Key, "CategoryEntitlementUpdated",
                        catalog[spec.AccessEntitlementId].Key,
                        $"{spec.IsDefaultForJoiner}|{spec.SortOrder}|{spec.IsActive}"), ct);
                }
            }
            else
            {
                AccessCategoryEntitlement created = AccessCategoryEntitlement.Create(
                    categoryId, spec.AccessEntitlementId, spec.IsDefaultForJoiner, spec.SortOrder,
                    clock.UtcNow, spec.IsActive);
                db.AccessCategoryEntitlements.Add(created);
                await businessAudit.AppendAsync(AccessAudit.Field(
                    category.Id, category.Key, "CategoryEntitlementAdded",
                    null, catalog[spec.AccessEntitlementId].Key, BusinessAuditAction.Created), ct);
            }
        }

        foreach (AccessCategoryEntitlement row in existing.Where(x => !keep.Contains(x.AccessEntitlementId)))
        {
            await businessAudit.AppendAsync(AccessAudit.Field(
                category.Id, category.Key, "CategoryEntitlementRemoved",
                existingKeys.GetValueOrDefault(row.AccessEntitlementId) ?? row.AccessEntitlementId.ToString(),
                null), ct);
            db.AccessCategoryEntitlements.Remove(row);
        }

        await db.SaveChangesAsync(ct);
        return await ListEntitlementsAsync(categoryId, ct);
    }

    public async Task EnsureCategoryEntitlementMappingAsync(
        Guid categoryId,
        Guid entitlementId,
        bool isDefaultForJoiner,
        int sortOrder,
        CancellationToken ct)
    {
        bool exists = await db.AccessCategoryEntitlements.AnyAsync(
            x => x.AccessCategoryId == categoryId && x.AccessEntitlementId == entitlementId, ct);
        if (exists) return; // do not overwrite DefaultForJoiner after first insert

        db.AccessCategoryEntitlements.Add(AccessCategoryEntitlement.Create(
            categoryId, entitlementId, isDefaultForJoiner, sortOrder, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    public async Task EnsureDevelopmentCatalogAsync(
        IReadOnlyDictionary<string, Guid> usersByUpn,
        CancellationToken ct)
    {
        await EnsureCategoryAsync(
            "IT_ACCESS",
            "IT Access",
            "الوصول إلى أنظمة تقنية المعلومات",
            "Systems and application access for QEC employees.",
            "الوصول إلى الأنظمة والتطبيقات لموظفي QEC.",
            preferSubject: true,
            requesters: Ups(usersByUpn, "demo.finance.manager@qec.local", "demo.hr.manager@qec.local"),
            approvers: Ups(usersByUpn, "demo.it.manager@qec.local"),
            fulfillers: Ups(usersByUpn, "demo.it.admin1@qec.local", "demo.it.admin2@qec.local"),
            verifiers: Ups(usersByUpn, "demo.it.admin1@qec.local"),
            closers: Ups(usersByUpn, "demo.it.admin1@qec.local", "demo.it.manager@qec.local"),
            ct);

        await EnsureCategoryAsync(
            "DOOR_ACCESS",
            "Door Access",
            "صلاحية الدخول إلى الأبواب",
            "Physical door and badge access.",
            "صلاحيات الدخول الفعلي والأبواب.",
            preferSubject: true,
            requesters: Ups(usersByUpn, "demo.hr.manager@qec.local"),
            approvers: Ups(usersByUpn, "demo.facilities@qec.local"),
            fulfillers: Ups(usersByUpn, "demo.facilities@qec.local"),
            verifiers: Ups(usersByUpn, "demo.facilities@qec.local"),
            closers: Ups(usersByUpn, "demo.facilities@qec.local"),
            ct);
    }

    private async Task EnsureCategoryAsync(
        string key,
        string nameEn,
        string nameAr,
        string? descriptionEn,
        string? descriptionAr,
        bool preferSubject,
        IReadOnlyList<Guid> requesters,
        IReadOnlyList<Guid> approvers,
        IReadOnlyList<Guid> fulfillers,
        IReadOnlyList<Guid> verifiers,
        IReadOnlyList<Guid> closers,
        CancellationToken ct)
    {
        AccessCategory? existing = await db.AccessCategories.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (existing is not null) return; // never overwrite admin edits

        AccessCategory created = AccessCategory.Create(
            key, nameEn, nameAr, clock.UtcNow, descriptionEn, descriptionAr, preferSubject, isActive: true);
        db.AccessCategories.Add(created);
        await db.SaveChangesAsync(ct);

        Dictionary<AccessCategoryStage, IReadOnlyList<Guid>> routing = new()
        {
            [AccessCategoryStage.Requester] = requesters,
            [AccessCategoryStage.Approver] = approvers,
            [AccessCategoryStage.Fulfiller] = fulfillers,
            [AccessCategoryStage.Verifier] = verifiers,
            [AccessCategoryStage.Closer] = closers,
        };
        await ReplaceRoutingAsync(created.Id, routing, ct);
    }

    private static IReadOnlyList<Guid> Ups(IReadOnlyDictionary<string, Guid> map, params string[] upns) =>
        upns.Where(map.ContainsKey).Select(u => map[u]).Distinct().ToList();

    private async Task<AccessCategoryDto> MapAsync(AccessCategory item, CancellationToken ct)
    {
        List<AccessCategoryParticipant> parts = await db.AccessCategoryParticipants.AsNoTracking()
            .Where(x => x.AccessCategoryId == item.Id).ToListAsync(ct);
        Dictionary<string, IReadOnlyList<Guid>> byStage = Enum.GetValues<AccessCategoryStage>()
            .ToDictionary(
                s => s.ToString(),
                s => (IReadOnlyList<Guid>)parts.Where(p => p.Stage == s).Select(p => p.UserId).ToList());
        return new(
            item.Id, item.Key, item.NameEn, item.NameAr, item.DescriptionEn, item.DescriptionAr,
            item.IsActive, item.PreferSubjectEmployeeVerification, item.CreatedAtUtc, item.UpdatedAtUtc,
            Convert.ToBase64String(item.RowVersion), byStage);
    }

    private static AccessCategoryEntitlementDto MapCategoryEntitlement(
        AccessCategoryEntitlement mapping,
        AccessEntitlement entitlement) =>
        new(
            mapping.Id,
            mapping.AccessCategoryId,
            mapping.AccessEntitlementId,
            entitlement.Key,
            entitlement.NameEn,
            entitlement.NameAr,
            entitlement.DefaultRevokeAction.ToString(),
            entitlement.IsPrivileged,
            mapping.IsDefaultForJoiner,
            mapping.SortOrder,
            mapping.IsActive,
            mapping.CreatedAtUtc,
            mapping.UpdatedAtUtc);
}
