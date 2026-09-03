using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;

namespace Qec.Itmg.Identity.Admin;

public sealed class AdminRolesService(
    IdentityDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISecurityAuditLogger securityAudit,
    ISharedDbTransaction sharedDbTransaction)
{
    public async Task<IReadOnlyList<AdminRoleDto>> ListAsync(CancellationToken cancellationToken)
    {
        List<Role> roles = await db.Roles.AsNoTracking()
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);

        return await MapRolesAsync(roles, includePermissions: false, cancellationToken);
    }

    public async Task<IResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Role? role = await db.Roles.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (role is null)
        {
            return AdminApiResults.NotFound("admin.roles.notFound", "Role was not found.");
        }

        AdminRoleDto dto = (await MapRolesAsync([role], includePermissions: true, cancellationToken))[0];
        return Results.Ok(dto);
    }

    public async Task<IResult> CreateAsync(CreateAdminRoleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return AdminApiResults.ValidationError("admin.roles.nameRequired", "Role name is required.", "name");
        }

        string name = request.Name.Trim();
        bool exists = await db.Roles.AsNoTracking().AnyAsync(role => role.Name == name, cancellationToken);
        if (exists)
        {
            return AdminApiResults.Conflict("admin.roles.nameConflict", "A role with this name already exists.");
        }

        Role role = Role.Create(name, clock.UtcNow, description: request.Description, isSystem: false);

        await sharedDbTransaction.ExecuteAsync(async ct =>
        {
            db.Roles.Add(role);
            await businessAudit.AppendAsync(AdminAuditComposer.RoleCreated(role), ct);
        }, cancellationToken);

        return Results.Created(
            $"/api/v1/admin/roles/{role.Id}",
            new AdminRoleDto(
                role.Id,
                role.Name,
                role.Description,
                role.IsSystem,
                AdminDtoMapper.ToBase64(role.RowVersion),
                PermissionCount: 0,
                Permissions: []));
    }

    public async Task<IResult> UpdateAsync(Guid id, UpdateAdminRoleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return AdminApiResults.ValidationError("admin.roles.nameRequired", "Role name is required.", "name");
        }

        if (!AdminDtoMapper.TryParseRowVersion(request.RowVersion, out _))
        {
            return AdminApiResults.ValidationError(
                "admin.roles.rowVersionInvalid",
                "Row version is required and must be base64.",
                "rowVersion");
        }

        Role? role = await db.Roles.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (role is null)
        {
            return AdminApiResults.NotFound("admin.roles.notFound", "Role was not found.");
        }

        if (!AdminDtoMapper.MatchesRowVersion(role.RowVersion, request.RowVersion))
        {
            return AdminApiResults.Conflict(
                "admin.roles.concurrencyConflict",
                "The role was modified by another request. Reload and try again.");
        }

        string name = request.Name.Trim();
        if (!role.IsSystem || string.Equals(role.Name, name, StringComparison.Ordinal))
        {
            bool nameTaken = await db.Roles.AsNoTracking()
                .AnyAsync(entity => entity.Name == name && entity.Id != id, cancellationToken);
            if (nameTaken)
            {
                return AdminApiResults.Conflict("admin.roles.nameConflict", "A role with this name already exists.");
            }
        }

        AdminAuditComposer.RoleAuditState before = AdminAuditComposer.CaptureRole(role);

        try
        {
            role.Update(name, request.Description, clock.UtcNow);
        }
        catch (InvalidOperationException exception)
        {
            return AdminApiResults.Conflict("admin.roles.systemImmutable", exception.Message);
        }

        List<BusinessAuditEntry> changes = AdminAuditComposer.RoleChanges(before, role).ToList();

        try
        {
            await sharedDbTransaction.ExecuteAsync(async ct =>
            {
                if (changes.Count > 0)
                {
                    await businessAudit.AppendManyAsync(changes, ct);
                }
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AdminApiResults.Conflict(
                "admin.roles.concurrencyConflict",
                "The role was modified by another request. Reload and try again.");
        }

        AdminRoleDto dto = (await MapRolesAsync([role], includePermissions: true, cancellationToken))[0];
        return Results.Ok(dto);
    }

    public async Task<IResult> ReplacePermissionsAsync(
        Guid id,
        ReplaceRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        Role? role = await db.Roles.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (role is null)
        {
            return AdminApiResults.NotFound("admin.roles.notFound", "Role was not found.");
        }

        Guid[] distinctPermissionIds = (request.PermissionIds ?? [])
            .Where(permissionId => permissionId != Guid.Empty)
            .Distinct()
            .ToArray();

        if (distinctPermissionIds.Length > 0)
        {
            int existingCount = await db.Permissions.AsNoTracking()
                .CountAsync(permission => distinctPermissionIds.Contains(permission.Id), cancellationToken);
            if (existingCount != distinctPermissionIds.Length)
            {
                return AdminApiResults.ValidationError(
                    "admin.roles.permissionIdsInvalid",
                    "One or more permission ids do not exist.",
                    "permissionIds");
            }
        }

        List<RolePermission> current = await db.RolePermissions
            .Where(rolePermission => rolePermission.RoleId == id)
            .ToListAsync(cancellationToken);

        HashSet<Guid> currentIds = current.Select(link => link.PermissionId).ToHashSet();
        HashSet<Guid> nextIds = distinctPermissionIds.ToHashSet();
        Guid[] added = nextIds.Except(currentIds).ToArray();
        Guid[] removed = currentIds.Except(nextIds).ToArray();

        Dictionary<Guid, string> permissionKeys = await db.Permissions.AsNoTracking()
            .Where(permission => currentIds.Contains(permission.Id) || nextIds.Contains(permission.Id))
            .ToDictionaryAsync(permission => permission.Id, permission => permission.Key, cancellationToken);

        await sharedDbTransaction.ExecuteAsync(async ct =>
        {
            db.RolePermissions.RemoveRange(current);

            foreach (Guid permissionId in distinctPermissionIds)
            {
                db.RolePermissions.Add(RolePermission.Create(id, permissionId));
            }

            foreach (Guid permissionId in added)
            {
                string key = permissionKeys[permissionId];
                await businessAudit.AppendAsync(
                    AdminAuditComposer.PermissionGranted(id, role.Name, permissionId, key),
                    ct);
                await securityAudit.AppendAsync(
                    new SecurityAuditEntry
                    {
                        EventType = SecurityEventType.PermissionGranted,
                        Outcome = SecurityEventOutcome.Success,
                        TargetType = nameof(Role),
                        TargetId = id.ToString("D"),
                        Details = $"Permission:{key}",
                    },
                    ct);
            }

            foreach (Guid permissionId in removed)
            {
                string key = permissionKeys[permissionId];
                await businessAudit.AppendAsync(
                    AdminAuditComposer.PermissionRevoked(id, role.Name, permissionId, key),
                    ct);
                await securityAudit.AppendAsync(
                    new SecurityAuditEntry
                    {
                        EventType = SecurityEventType.PermissionRevoked,
                        Outcome = SecurityEventOutcome.Success,
                        TargetType = nameof(Role),
                        TargetId = id.ToString("D"),
                        Details = $"Permission:{key}",
                    },
                    ct);
            }
        }, cancellationToken);

        Role refreshed = await db.Roles.AsNoTracking().SingleAsync(entity => entity.Id == id, cancellationToken);
        AdminRoleDto dto = (await MapRolesAsync([refreshed], includePermissions: true, cancellationToken))[0];
        return Results.Ok(dto);
    }

    public async Task<IReadOnlyList<AdminPermissionDto>> ListPermissionsAsync(CancellationToken cancellationToken)
    {
        return await db.Permissions.AsNoTracking()
            .OrderBy(permission => permission.Key)
            .Select(permission => new AdminPermissionDto(permission.Id, permission.Key, permission.Description))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<AdminRoleDto>> MapRolesAsync(
        IReadOnlyList<Role> roles,
        bool includePermissions,
        CancellationToken cancellationToken)
    {
        if (roles.Count == 0)
        {
            return [];
        }

        Guid[] roleIds = roles.Select(role => role.Id).ToArray();
        var permissionLinks = await (
            from rolePermission in db.RolePermissions.AsNoTracking()
            join permission in db.Permissions.AsNoTracking() on rolePermission.PermissionId equals permission.Id
            where roleIds.Contains(rolePermission.RoleId)
            select new
            {
                rolePermission.RoleId,
                Permission = new AdminPermissionDto(permission.Id, permission.Key, permission.Description),
            })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, List<AdminPermissionDto>> permissionsByRole = permissionLinks
            .GroupBy(link => link.RoleId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(link => link.Permission).OrderBy(permission => permission.Key).ToList());

        return roles
            .Select(role =>
            {
                List<AdminPermissionDto> permissions = permissionsByRole.TryGetValue(role.Id, out List<AdminPermissionDto>? list)
                    ? list
                    : [];
                return new AdminRoleDto(
                    role.Id,
                    role.Name,
                    role.Description,
                    role.IsSystem,
                    AdminDtoMapper.ToBase64(role.RowVersion),
                    permissions.Count,
                    includePermissions ? permissions : []);
            })
            .ToList();
    }
}
