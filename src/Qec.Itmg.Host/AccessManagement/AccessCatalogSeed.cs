using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qec.Itmg.AccessManagement.Domain;
using Qec.Itmg.AccessManagement.Persistence;
using Qec.Itmg.AccessManagement.Services;
using Qec.Itmg.BuildingBlocks.Time;

namespace Qec.Itmg.Host.AccessManagement;

public interface IAccessCatalogSeedRunner
{
    Task RunAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Idempotent starter Access Entitlement catalog and category mappings for all environments.
/// Does not seed fake users or current-access registers.
/// </summary>
public sealed class AccessCatalogSeedRunner(
    AccessManagementDbContext db,
    AccessCategoryService categories,
    IClock clock,
    ILogger<AccessCatalogSeedRunner> logger) : IAccessCatalogSeedRunner
{
    private static readonly (string Key, string NameEn, string NameAr, AccessRevokeAction Revoke, bool Privileged)[] Catalog =
    [
        ("GOOGLE_WORKSPACE", "Google Workspace / Gmail", "مساحة عمل Google / البريد", AccessRevokeAction.Disable, false),
        ("AD_DOMAIN_USER", "Active Directory Domain User", "مستخدم نطاق Active Directory", AccessRevokeAction.Disable, false),
        ("COMPANY_SHARED_FOLDER", "Company Shared Folder", "المجلد المشترك للشركة", AccessRevokeAction.Remove, false),
        ("ATTENDANCE_SYSTEM", "Attendance System", "نظام الحضور", AccessRevokeAction.Disable, false),
        ("DOOR_ACCESS", "Door Access", "صلاحية الدخول إلى الأبواب", AccessRevokeAction.Remove, false),
        ("VPN_ACCESS", "VPN Access", "الوصول عبر VPN", AccessRevokeAction.Remove, false),
        ("ERP_ACCESS", "ERP Access", "صلاحية نظام ERP", AccessRevokeAction.Remove, false),
        ("CCTV_ACCESS", "CCTV Access", "صلاحية كاميرات المراقبة", AccessRevokeAction.Remove, false),
    ];

    private static readonly (string Key, bool DefaultForJoiner, int SortOrder)[] ItAccessDefaults =
    [
        ("GOOGLE_WORKSPACE", true, 10),
        ("AD_DOMAIN_USER", true, 20),
        ("COMPANY_SHARED_FOLDER", true, 30),
        ("ATTENDANCE_SYSTEM", true, 40),
        ("DOOR_ACCESS", true, 50),
        ("VPN_ACCESS", false, 60),
        ("ERP_ACCESS", false, 70),
        ("CCTV_ACCESS", false, 80),
    ];

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<string, Guid> byKey = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string nameEn, string nameAr, AccessRevokeAction revoke, bool privileged) in Catalog)
        {
            AccessEntitlement? existing = await db.AccessEntitlements.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
            if (existing is null)
            {
                existing = AccessEntitlement.Create(
                    key, nameEn, nameAr, revoke, clock.UtcNow, isPrivileged: privileged, isActive: true);
                db.AccessEntitlements.Add(existing);
                await db.SaveChangesAsync(cancellationToken);
            }

            byKey[key] = existing.Id;
        }

        AccessCategory? itAccess = await db.AccessCategories.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == "IT_ACCESS", cancellationToken);
        if (itAccess is not null)
        {
            foreach ((string key, bool defaultForJoiner, int sortOrder) in ItAccessDefaults)
            {
                if (!byKey.TryGetValue(key, out Guid entitlementId)) continue;
                await categories.EnsureCategoryEntitlementMappingAsync(
                    itAccess.Id, entitlementId, defaultForJoiner, sortOrder, cancellationToken);
            }
        }

        AccessCategory? door = await db.AccessCategories.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == "DOOR_ACCESS", cancellationToken);
        if (door is not null && byKey.TryGetValue("DOOR_ACCESS", out Guid doorEntitlementId))
        {
            await categories.EnsureCategoryEntitlementMappingAsync(
                door.Id, doorEntitlementId, isDefaultForJoiner: false, sortOrder: 10, cancellationToken);
        }

        logger.LogInformation("Access entitlement catalog seed completed ({Count} entitlements).", Catalog.Length);
    }
}
