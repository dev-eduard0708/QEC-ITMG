using Microsoft.EntityFrameworkCore;
using Qec.Itmg.AccessManagement.Domain;
using Qec.Itmg.AccessManagement.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;

namespace Qec.Itmg.AccessManagement.Services;

public sealed record AccessEntitlementDto(
    Guid Id,
    string Key,
    string NameEn,
    string NameAr,
    string? DescriptionEn,
    string? DescriptionAr,
    string DefaultRevokeAction,
    bool IsPrivileged,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string RowVersion);

public sealed class AccessEntitlementService(
    AccessManagementDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit)
{
    public async Task<IReadOnlyList<AccessEntitlementDto>> ListAsync(
        bool activeOnly,
        string? search,
        CancellationToken ct)
    {
        IQueryable<AccessEntitlement> q = db.AccessEntitlements.AsNoTracking();
        if (activeOnly) q = q.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(x =>
                x.Key.Contains(term)
                || x.NameEn.Contains(term)
                || x.NameAr.Contains(term)
                || (x.DescriptionEn != null && x.DescriptionEn.Contains(term))
                || (x.DescriptionAr != null && x.DescriptionAr.Contains(term)));
        }

        List<AccessEntitlement> items = await q.OrderBy(x => x.Key).ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<AccessEntitlementDto?> GetAsync(Guid id, CancellationToken ct)
    {
        AccessEntitlement? item = await db.AccessEntitlements.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<AccessEntitlementDto> CreateAsync(
        string key,
        string nameEn,
        string nameAr,
        string? descriptionEn,
        string? descriptionAr,
        AccessRevokeAction defaultRevokeAction,
        bool isPrivileged,
        bool isActive,
        CancellationToken ct)
    {
        string normalizedKey = key.Trim().ToUpperInvariant();
        bool exists = await db.AccessEntitlements.AnyAsync(x => x.Key == normalizedKey, ct);
        if (exists) throw new InvalidOperationException($"Entitlement key '{normalizedKey}' already exists.");

        AccessEntitlement entity = AccessEntitlement.Create(
            normalizedKey, nameEn, nameAr, defaultRevokeAction, clock.UtcNow,
            descriptionEn, descriptionAr, isPrivileged, isActive);
        db.AccessEntitlements.Add(entity);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.Key, "AccessEntitlementCreated", null, entity.NameEn,
            BusinessAuditAction.Created), ct);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<AccessEntitlementDto> UpdateAsync(
        Guid id,
        string nameEn,
        string nameAr,
        string? descriptionEn,
        string? descriptionAr,
        AccessRevokeAction defaultRevokeAction,
        bool isPrivileged,
        bool isActive,
        CancellationToken ct)
    {
        AccessEntitlement entity = await db.AccessEntitlements.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Access entitlement not found.");
        entity.Update(nameEn, nameAr, descriptionEn, descriptionAr, defaultRevokeAction, isPrivileged, isActive, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            entity.Id, entity.Key, "AccessEntitlementUpdated", null, entity.NameEn), ct);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    private static AccessEntitlementDto Map(AccessEntitlement x) =>
        new(x.Id, x.Key, x.NameEn, x.NameAr, x.DescriptionEn, x.DescriptionAr,
            x.DefaultRevokeAction.ToString(), x.IsPrivileged, x.IsActive,
            x.CreatedAtUtc, x.UpdatedAtUtc, Convert.ToBase64String(x.RowVersion));
}
