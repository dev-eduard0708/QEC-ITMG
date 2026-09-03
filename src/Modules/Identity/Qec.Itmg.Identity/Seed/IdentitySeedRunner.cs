using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Identity.Admin;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;

namespace Qec.Itmg.Identity.Seed;

public interface IIdentitySeedRunner
{
    Task RunAsync(CancellationToken cancellationToken = default);
}

public sealed class IdentitySeedRunner(
    IdentityDbContext db,
    IClock clock,
    IOptions<IdentitySeedOptions> options,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction,
    ILogger<IdentitySeedRunner> logger) : IIdentitySeedRunner
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await sharedDbTransaction.ExecuteAsync(async ct =>
        {
            Dictionary<string, Permission> permissions = await EnsurePermissionsAsync(ct);
            await EnsureRoleAsync(
                IdentitySeedCatalog.EmployeeRoleName,
                "Default role for Google JIT and standard users. No admin permissions.",
                ct);
            Role platformAdmin = await EnsureRoleAsync(
                IdentitySeedCatalog.PlatformAdministratorRoleName,
                "System administrator role. Authorization is via assigned admin.* permissions only.",
                ct);

            await EnsureRolePermissionsAsync(platformAdmin, permissions.Values, ct);
            await EnsurePlatformAdministratorBootstrapAsync(platformAdmin, ct);

            logger.LogInformation(
                "Identity seed completed. Permissions={PermissionCount} Roles=Employee,PlatformAdministrator",
                permissions.Count);
        }, cancellationToken);
    }

    private async Task<Dictionary<string, Permission>> EnsurePermissionsAsync(CancellationToken cancellationToken)
    {
        string[] keys = IdentitySeedCatalog.SystemPermissions.Select(static item => item.Key).ToArray();
        Dictionary<string, Permission> existing = await db.Permissions
            .Where(permission => keys.Contains(permission.Key))
            .ToDictionaryAsync(permission => permission.Key, cancellationToken);

        foreach ((string key, string description) in IdentitySeedCatalog.SystemPermissions)
        {
            if (existing.ContainsKey(key))
            {
                continue;
            }

            Permission permission = Permission.Create(key, description);
            db.Permissions.Add(permission);
            existing[key] = permission;
        }

        return existing;
    }

    private async Task<Role> EnsureRoleAsync(string name, string description, CancellationToken cancellationToken)
    {
        Role? role = await db.Roles.FirstOrDefaultAsync(candidate => candidate.Name == name, cancellationToken);
        if (role is not null)
        {
            return role;
        }

        role = Role.Create(name, clock.UtcNow, description, isSystem: true);
        db.Roles.Add(role);
        return role;
    }

    private async Task EnsureRolePermissionsAsync(
        Role platformAdmin,
        IEnumerable<Permission> permissions,
        CancellationToken cancellationToken)
    {
        HashSet<Guid> linked = (await db.RolePermissions
            .AsNoTracking()
            .Where(link => link.RoleId == platformAdmin.Id)
            .Select(link => link.PermissionId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (Permission permission in permissions)
        {
            if (linked.Contains(permission.Id))
            {
                continue;
            }

            db.RolePermissions.Add(RolePermission.Create(platformAdmin.Id, permission.Id));
            await businessAudit.AppendAsync(
                AdminAuditComposer.PermissionGranted(
                    platformAdmin.Id,
                    platformAdmin.Name,
                    permission.Id,
                    permission.Key),
                cancellationToken);
        }
    }

    private async Task EnsurePlatformAdministratorBootstrapAsync(
        Role platformAdmin,
        CancellationToken cancellationToken)
    {
        string? upn = options.Value.PlatformAdministratorUpn?.Trim();
        if (string.IsNullOrWhiteSpace(upn))
        {
            return;
        }

        string displayName = string.IsNullOrWhiteSpace(options.Value.PlatformAdministratorDisplayName)
            ? upn
            : options.Value.PlatformAdministratorDisplayName.Trim();

        User? user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Upn == upn, cancellationToken);
        if (user is null)
        {
            user = User.Create(upn, displayName, UserType.Employee, clock.UtcNow);
            db.Users.Add(user);
            await businessAudit.AppendAsync(AdminAuditComposer.UserCreated(user), cancellationToken);
            logger.LogInformation("Bootstrap Platform Administrator user pre-provisioned for configured UPN.");
        }
        else
        {
            if (user.Status != UserStatus.Active)
            {
                user.Enable(clock.UtcNow);
                logger.LogInformation("Bootstrap Platform Administrator user re-enabled for configured UPN.");
            }

            if (!string.Equals(user.DisplayName, displayName, StringComparison.Ordinal))
            {
                user.Rename(displayName, clock.UtcNow);
            }
        }

        bool alreadyAssigned = await db.UserRoles.AnyAsync(
            link => link.UserId == user.Id && link.RoleId == platformAdmin.Id,
            cancellationToken);
        if (alreadyAssigned)
        {
            return;
        }

        db.UserRoles.Add(UserRole.Create(user.Id, platformAdmin.Id, clock.UtcNow));
        await businessAudit.AppendAsync(
            AdminAuditComposer.RoleAssigned(user.Id, user.Upn, platformAdmin.Id, platformAdmin.Name),
            cancellationToken);
    }
}
