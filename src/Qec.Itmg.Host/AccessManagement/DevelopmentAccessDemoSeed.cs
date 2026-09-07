using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qec.Itmg.AccessManagement.Domain;
using Qec.Itmg.AccessManagement.Persistence;
using Qec.Itmg.AccessManagement.Services;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Identity.Seed;

namespace Qec.Itmg.Host.AccessManagement;

/// <summary>Development-only demo personas and access category routing. Never runs outside Development.</summary>
public interface IDevelopmentAccessDemoSeedRunner
{
    Task RunAsync(CancellationToken cancellationToken = default);
}

public sealed class DevelopmentAccessDemoSeedRunner(
    IHostEnvironment environment,
    IdentityDbContext identityDb,
    AccessManagementDbContext accessDb,
    AccessCategoryService categories,
    AccessEntitlementService entitlements,
    UserAccessService userAccess,
    IAccessCatalogSeedRunner catalogSeed,
    IClock clock,
    ILogger<DevelopmentAccessDemoSeedRunner> logger) : IDevelopmentAccessDemoSeedRunner
{
    public static readonly (string Key, string Upn, string DisplayName, string[] Permissions)[] Personas =
    [
        ("finance-manager", "demo.finance.manager@qec.local", "Demo Finance Manager", ["access.request", "admin.users"]),
        ("hr-manager", "demo.hr.manager@qec.local", "Demo HR Manager", ["access.request", "admin.users"]),
        ("it-manager", "demo.it.manager@qec.local", "Demo IT Manager", ["access.request", "access.approve", "access.configure", "access.fulfill", "admin.users"]),
        ("it-admin1", "demo.it.admin1@qec.local", "Demo IT Admin 1", ["access.request", "access.fulfill", "access.configure", "admin.users"]),
        ("it-admin2", "demo.it.admin2@qec.local", "Demo IT Admin 2", ["access.fulfill"]),
        ("finance-employee", "demo.finance.employee@qec.local", "Demo Finance Employee", ["access.request"]),
        ("facilities", "demo.facilities@qec.local", "Demo Facilities Officer", ["access.request", "access.approve", "access.fulfill", "admin.users"]),
    ];

    private static readonly (string Key, string NameEn, string NameAr, AccessRevokeAction Revoke)[] DevEntitlements =
    [
        ("FINANCE_SHARED_FOLDER", "Finance Shared Folder", "المجلد المشترك للمالية", AccessRevokeAction.Remove),
        ("HR_SHARED_FOLDER", "HR Shared Folder", "المجلد المشترك للموارد البشرية", AccessRevokeAction.Remove),
    ];

    private static readonly string[] FinanceEmployeeActiveKeys =
    [
        "GOOGLE_WORKSPACE",
        "AD_DOMAIN_USER",
        "COMPANY_SHARED_FOLDER",
        "ATTENDANCE_SYSTEM",
        "DOOR_ACCESS",
        "ERP_ACCESS",
        "FINANCE_SHARED_FOLDER",
    ];

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
        {
            logger.LogDebug("Skipping Development access demo seed outside Development.");
            return;
        }

        Dictionary<string, Permission> permissions = await identityDb.Permissions
            .ToDictionaryAsync(x => x.Key, cancellationToken);

        Dictionary<string, Guid> usersByUpn = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string upn, string displayName, string[] permKeys) in Personas)
        {
            Role role = await EnsureRoleAsync($"Demo · {displayName}", $"Development demo role for {displayName}.", cancellationToken);
            List<Permission> rolePerms = permKeys
                .Where(permissions.ContainsKey)
                .Select(k => permissions[k])
                .ToList();
            await EnsureRolePermissionsAsync(role, rolePerms, cancellationToken);

            User user = await EnsureUserAsync(upn, displayName, $"dev:demo:{key}", cancellationToken);
            await EnsureUserRoleAsync(user, role, cancellationToken);
            usersByUpn[upn] = user.Id;
        }

        await identityDb.SaveChangesAsync(cancellationToken);
        await categories.EnsureDevelopmentCatalogAsync(usersByUpn, cancellationToken);

        // Categories now exist — ensure global catalog mappings, then Dev-only extras.
        await catalogSeed.RunAsync(cancellationToken);
        await EnsureDevEntitlementsAndMappingsAsync(cancellationToken);
        await EnsureFinanceEmployeeCurrentAccessAsync(usersByUpn, cancellationToken);

        logger.LogInformation("Development access demo personas and categories ensured ({Count} users).", Personas.Length);
    }

    private async Task EnsureDevEntitlementsAndMappingsAsync(CancellationToken ct)
    {
        AccessCategory? itAccess = await accessDb.AccessCategories.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == "IT_ACCESS", ct);
        if (itAccess is null) return;

        int sort = 90;
        foreach ((string key, string nameEn, string nameAr, AccessRevokeAction revoke) in DevEntitlements)
        {
            AccessEntitlement? existing = await accessDb.AccessEntitlements.FirstOrDefaultAsync(x => x.Key == key, ct);
            Guid entitlementId;
            if (existing is null)
            {
                AccessEntitlementDto created = await entitlements.CreateAsync(
                    key, nameEn, nameAr, null, null, revoke, isPrivileged: false, isActive: true, ct);
                entitlementId = created.Id;
            }
            else
            {
                entitlementId = existing.Id;
            }

            await categories.EnsureCategoryEntitlementMappingAsync(
                itAccess.Id, entitlementId, isDefaultForJoiner: false, sortOrder: sort, ct);
            sort += 10;
        }
    }

    private async Task EnsureFinanceEmployeeCurrentAccessAsync(
        IReadOnlyDictionary<string, Guid> usersByUpn,
        CancellationToken ct)
    {
        if (!usersByUpn.TryGetValue("demo.finance.employee@qec.local", out Guid userId))
            return;

        Dictionary<string, AccessEntitlement> byKey = await accessDb.AccessEntitlements.AsNoTracking()
            .Where(x => FinanceEmployeeActiveKeys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, ct);

        foreach (string key in FinanceEmployeeActiveKeys)
        {
            if (!byKey.TryGetValue(key, out AccessEntitlement? entitlement)) continue;
            await userAccess.EnsureActiveAsync(userId, entitlement.Id, entitlement.Key, entitlement.NameEn, ct);
        }

        await accessDb.SaveChangesAsync(ct);
    }

    private async Task<Role> EnsureRoleAsync(string name, string description, CancellationToken ct)
    {
        Role? role = await identityDb.Roles.FirstOrDefaultAsync(x => x.Name == name, ct);
        if (role is not null) return role;
        role = Role.Create(name, clock.UtcNow, description, isSystem: false);
        identityDb.Roles.Add(role);
        await identityDb.SaveChangesAsync(ct);
        return role;
    }

    private async Task EnsureRolePermissionsAsync(Role role, IEnumerable<Permission> permissions, CancellationToken ct)
    {
        List<Permission> desired = permissions.ToList();
        HashSet<Guid> desiredIds = desired.Select(p => p.Id).ToHashSet();
        List<RolePermission> existingRows = await identityDb.RolePermissions
            .Where(x => x.RoleId == role.Id)
            .ToListAsync(ct);

        foreach (RolePermission row in existingRows)
        {
            if (!desiredIds.Contains(row.PermissionId))
                identityDb.RolePermissions.Remove(row);
        }

        HashSet<Guid> remaining = existingRows
            .Where(x => desiredIds.Contains(x.PermissionId))
            .Select(x => x.PermissionId)
            .ToHashSet();
        foreach (Permission permission in desired)
        {
            if (remaining.Contains(permission.Id)) continue;
            identityDb.RolePermissions.Add(RolePermission.Create(role.Id, permission.Id));
        }
    }

    private async Task<User> EnsureUserAsync(string upn, string displayName, string externalId, CancellationToken ct)
    {
        User? user = await identityDb.Users.FirstOrDefaultAsync(
            x => x.Upn == upn || x.DirectoryObjectId == externalId, ct);
        if (user is null)
        {
            user = User.Create(upn, displayName, UserType.Employee, clock.UtcNow, directoryObjectId: externalId);
            identityDb.Users.Add(user);
            await identityDb.SaveChangesAsync(ct);
            return user;
        }

        if (user.Status != UserStatus.Active)
            user.Enable(clock.UtcNow);
        return user;
    }

    private async Task EnsureUserRoleAsync(User user, Role role, CancellationToken ct)
    {
        bool has = await identityDb.UserRoles.AnyAsync(x => x.UserId == user.Id && x.RoleId == role.Id, ct);
        if (has) return;
        identityDb.UserRoles.Add(UserRole.Create(user.Id, role.Id, clock.UtcNow));
    }
}
