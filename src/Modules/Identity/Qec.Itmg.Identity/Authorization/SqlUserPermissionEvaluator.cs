using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Qec.Itmg.Identity.Authentication;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Identity.Seed;

namespace Qec.Itmg.Identity.Authorization;

public interface IUserPermissionEvaluator
{
    Task<bool> HasPermissionAsync(
        ClaimsPrincipal principal,
        string permissionKey,
        CancellationToken cancellationToken = default);
}

public sealed class SqlUserPermissionEvaluator(IdentityDbContext dbContext) : IUserPermissionEvaluator
{
    public async Task<bool> HasPermissionAsync(
        ClaimsPrincipal principal,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

        if (principal.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        // Never authorize from IdP role/group claims.
        if (OidcPrincipalMapper.ContainsAuthorizationRoleClaims(principal))
        {
            return false;
        }

        string normalizedKey = permissionKey.Trim().ToLowerInvariant();
        Guid? userId = await ResolveActiveUserIdAsync(principal, cancellationToken);
        if (userId is null)
        {
            return false;
        }

        // Platform Administrator always has every catalog permission (even before role links catch up).
        bool isPlatformAdmin = await dbContext.UserRoles
            .AsNoTracking()
            .AnyAsync(
                userRole =>
                    userRole.UserId == userId.Value
                    && userRole.Role.Name == IdentitySeedCatalog.PlatformAdministratorRoleName,
                cancellationToken);
        if (isPlatformAdmin)
        {
            return await dbContext.Permissions
                .AsNoTracking()
                .AnyAsync(permission => permission.Key == normalizedKey, cancellationToken);
        }

        return await dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == userId.Value)
            .SelectMany(userRole => userRole.Role.RolePermissions)
            .AnyAsync(
                rolePermission => rolePermission.Permission.Key == normalizedKey,
                cancellationToken);
    }

    private async Task<Guid?> ResolveActiveUserIdAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        string? externalId = principal.FindFirstValue(OidcPrincipalMapper.ExternalIdClaimType)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(externalId))
        {
            Guid? byDirectory = await dbContext.Users
                .AsNoTracking()
                .Where(user => user.DirectoryObjectId == externalId && user.Status == UserStatus.Active)
                .Select(user => (Guid?)user.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (byDirectory is not null)
            {
                return byDirectory;
            }
        }

        string? upn = principal.FindFirstValue(OidcPrincipalMapper.UpnClaimType)
            ?? principal.FindFirstValue(ClaimTypes.Upn);

        if (string.IsNullOrWhiteSpace(upn))
        {
            return null;
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Upn == upn && user.Status == UserStatus.Active)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
