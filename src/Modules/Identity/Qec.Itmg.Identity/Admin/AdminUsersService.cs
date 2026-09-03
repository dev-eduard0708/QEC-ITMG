using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;

namespace Qec.Itmg.Identity.Admin;

public sealed class AdminUsersService(IdentityDbContext db, IClock clock)
{
    public async Task<IReadOnlyList<AdminUserDto>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        IQueryable<User> query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(user =>
                user.Upn.Contains(term) || user.DisplayName.Contains(term));
        }

        List<User> users = await query
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Upn)
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            return [];
        }

        Guid[] userIds = users.Select(user => user.Id).ToArray();
        var roleLinks = await (
            from userRole in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new { userRole.UserId, Role = new AdminRoleSummaryDto(role.Id, role.Name) })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, List<AdminRoleSummaryDto>> rolesByUser = roleLinks
            .GroupBy(link => link.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(link => link.Role).OrderBy(role => role.Name).ToList());

        return users
            .Select(user => MapUser(
                user,
                rolesByUser.TryGetValue(user.Id, out List<AdminRoleSummaryDto>? roles) ? roles : []))
            .ToList();
    }

    public async Task<IResult> CreateAsync(CreateAdminUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Upn))
        {
            return AdminApiResults.ValidationError("admin.users.upnRequired", "UPN is required.", "upn");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return AdminApiResults.ValidationError(
                "admin.users.displayNameRequired",
                "Display name is required.",
                "displayName");
        }

        if (!AdminDtoMapper.TryParseUserType(request.UserType, out UserType userType))
        {
            return AdminApiResults.ValidationError(
                "admin.users.userTypeInvalid",
                "User type must be Employee, Vendor, or Service.",
                "userType");
        }

        string upn = request.Upn.Trim();
        bool exists = await db.Users.AsNoTracking().AnyAsync(user => user.Upn == upn, cancellationToken);
        if (exists)
        {
            return AdminApiResults.Conflict("admin.users.upnConflict", "A user with this UPN already exists.");
        }

        if (!string.IsNullOrWhiteSpace(request.DirectoryObjectId))
        {
            string directoryObjectId = request.DirectoryObjectId.Trim();
            bool directoryTaken = await db.Users.AsNoTracking()
                .AnyAsync(user => user.DirectoryObjectId == directoryObjectId, cancellationToken);
            if (directoryTaken)
            {
                return AdminApiResults.Conflict(
                    "admin.users.directoryObjectIdConflict",
                    "A user with this directory object id already exists.");
            }
        }

        User user = User.Create(
            upn,
            request.DisplayName,
            userType,
            clock.UtcNow,
            directoryObjectId: request.DirectoryObjectId,
            timeZone: request.TimeZone);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/v1/admin/users/{user.Id}", MapUser(user, []));
    }

    public async Task<IResult> UpdateAsync(Guid id, UpdateAdminUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return AdminApiResults.ValidationError(
                "admin.users.displayNameRequired",
                "Display name is required.",
                "displayName");
        }

        if (!AdminDtoMapper.TryParseUserType(request.UserType, out UserType userType))
        {
            return AdminApiResults.ValidationError(
                "admin.users.userTypeInvalid",
                "User type must be Employee, Vendor, or Service.",
                "userType");
        }

        if (!AdminDtoMapper.TryParseUserStatus(request.Status, out UserStatus status))
        {
            return AdminApiResults.ValidationError(
                "admin.users.statusInvalid",
                "Status must be Active or Disabled.",
                "status");
        }

        if (!AdminDtoMapper.TryParseRowVersion(request.RowVersion, out _))
        {
            return AdminApiResults.ValidationError(
                "admin.users.rowVersionInvalid",
                "Row version is required and must be base64.",
                "rowVersion");
        }

        User? user = await db.Users.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (user is null)
        {
            return AdminApiResults.NotFound("admin.users.notFound", "User was not found.");
        }

        if (!AdminDtoMapper.MatchesRowVersion(user.RowVersion, request.RowVersion))
        {
            return AdminApiResults.Conflict(
                "admin.users.concurrencyConflict",
                "The user was modified by another request. Reload and try again.");
        }

        if (!string.IsNullOrWhiteSpace(request.DirectoryObjectId))
        {
            string directoryObjectId = request.DirectoryObjectId.Trim();
            bool directoryTaken = await db.Users.AsNoTracking()
                .AnyAsync(
                    entity => entity.DirectoryObjectId == directoryObjectId && entity.Id != id,
                    cancellationToken);
            if (directoryTaken)
            {
                return AdminApiResults.Conflict(
                    "admin.users.directoryObjectIdConflict",
                    "A user with this directory object id already exists.");
            }
        }

        user.UpdateProfile(
            request.DisplayName,
            userType,
            status,
            clock.UtcNow,
            timeZone: request.TimeZone,
            directoryObjectId: request.DirectoryObjectId);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AdminApiResults.Conflict(
                "admin.users.concurrencyConflict",
                "The user was modified by another request. Reload and try again.");
        }

        List<AdminRoleSummaryDto> roles = await LoadRolesAsync(user.Id, cancellationToken);
        return Results.Ok(MapUser(user, roles));
    }

    public async Task<IResult> ReplaceRolesAsync(
        Guid id,
        ReplaceUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        User? user = await db.Users.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (user is null)
        {
            return AdminApiResults.NotFound("admin.users.notFound", "User was not found.");
        }

        Guid[] distinctRoleIds = (request.RoleIds ?? [])
            .Where(roleId => roleId != Guid.Empty)
            .Distinct()
            .ToArray();

        if (distinctRoleIds.Length > 0)
        {
            int existingCount = await db.Roles.AsNoTracking()
                .CountAsync(role => distinctRoleIds.Contains(role.Id), cancellationToken);
            if (existingCount != distinctRoleIds.Length)
            {
                return AdminApiResults.ValidationError(
                    "admin.users.roleIdsInvalid",
                    "One or more role ids do not exist.",
                    "roleIds");
            }
        }

        List<UserRole> current = await db.UserRoles
            .Where(userRole => userRole.UserId == id)
            .ToListAsync(cancellationToken);

        db.UserRoles.RemoveRange(current);

        DateTimeOffset assignedAt = clock.UtcNow;
        foreach (Guid roleId in distinctRoleIds)
        {
            db.UserRoles.Add(UserRole.Create(id, roleId, assignedAt));
        }

        await db.SaveChangesAsync(cancellationToken);

        List<AdminRoleSummaryDto> roles = await LoadRolesAsync(id, cancellationToken);
        User refreshed = await db.Users.AsNoTracking().SingleAsync(entity => entity.Id == id, cancellationToken);
        return Results.Ok(MapUser(refreshed, roles));
    }

    private async Task<List<AdminRoleSummaryDto>> LoadRolesAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await (
            from userRole in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == userId
            orderby role.Name
            select new AdminRoleSummaryDto(role.Id, role.Name))
            .ToListAsync(cancellationToken);
    }

    private static AdminUserDto MapUser(User user, IReadOnlyList<AdminRoleSummaryDto> roles) =>
        new(
            user.Id,
            user.Upn,
            user.DisplayName,
            user.Status.ToString(),
            user.UserType.ToString(),
            user.DirectoryObjectId,
            user.TimeZone,
            AdminDtoMapper.ToBase64(user.RowVersion),
            roles);
}
