using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Identity.Admin;
using Qec.Itmg.Identity.Authentication;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Identity.Seed;

namespace Qec.Itmg.Identity.CurrentUser;

public interface ICurrentUserService
{
    Task<CurrentUserDto?> GetSessionAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}

public sealed class CurrentUserService(
    IdentityDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction,
    IHostEnvironment environment,
    IOptions<OidcAuthenticationOptions> oidcOptions) : ICurrentUserService
{
    public const string EmployeeRoleName = IdentitySeedCatalog.EmployeeRoleName;

    public async Task<CurrentUserDto?> GetSessionAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        if (OidcPrincipalMapper.ContainsAuthorizationRoleClaims(principal))
        {
            return null;
        }

        bool isBreakGlass = BreakGlassPrincipalFactory.IsBreakGlass(principal);
        string authMethod = isBreakGlass
            ? BreakGlassPrincipalFactory.AuthMethodBreakGlass
            : DevelopmentLoginPrincipalFactory.IsDevelopment(principal)
                ? DevelopmentLoginPrincipalFactory.AuthMethodDevelopment
                : "Google";

        string? externalId = principal.FindFirstValue(OidcPrincipalMapper.ExternalIdClaimType)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        string? upn = principal.FindFirstValue(OidcPrincipalMapper.UpnClaimType)
            ?? principal.FindFirstValue(ClaimTypes.Upn)
            ?? principal.FindFirstValue(ClaimTypes.Email);
        string? displayName = principal.FindFirstValue(ClaimTypes.Name);

        User? user = await ResolveUserAsync(
            externalId,
            upn,
            displayName,
            allowJit: AllowGoogleJitProvisioning(isBreakGlass),
            cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
        {
            return null;
        }

        string? claimAvatar = principal.FindFirstValue(OidcPrincipalMapper.AvatarUrlClaimType);
        if (!isBreakGlass && !DevelopmentLoginPrincipalFactory.IsDevelopment(principal))
        {
            await SyncGoogleProfileAsync(user, displayName, claimAvatar, cancellationToken);
        }

        string? avatarUrl = !string.IsNullOrWhiteSpace(user.ProfileImageUrl)
            ? user.ProfileImageUrl
            : claimAvatar;
        return await MapSessionAsync(user, authMethod, avatarUrl, cancellationToken);
    }

    private async Task SyncGoogleProfileAsync(
        User user,
        string? displayName,
        string? profileImageUrl,
        CancellationToken cancellationToken)
    {
        string nextName = string.IsNullOrWhiteSpace(displayName) ? user.DisplayName : displayName.Trim();
        string? beforeImage = user.ProfileImageUrl;
        string beforeName = user.DisplayName;
        user.SyncGoogleProfile(nextName, profileImageUrl, clock.UtcNow);
        if (string.Equals(beforeName, user.DisplayName, StringComparison.Ordinal)
            && string.Equals(beforeImage, user.ProfileImageUrl, StringComparison.Ordinal))
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private bool AllowGoogleJitProvisioning(bool isBreakGlass)
    {
        if (isBreakGlass)
        {
            return false;
        }

        // Development-only switch; Production continues existing domain-gated JIT (Employee role only).
        if (environment.IsDevelopment())
        {
            return oidcOptions.Value.DevelopmentAutoProvisionEmployee;
        }

        return true;
    }

    private async Task<User?> ResolveUserAsync(
        string? externalId,
        string? upn,
        string? displayName,
        bool allowJit,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(externalId))
        {
            User? byDirectory = await db.Users
                .FirstOrDefaultAsync(
                    candidate => candidate.DirectoryObjectId == externalId,
                    cancellationToken);
            if (byDirectory is not null)
            {
                return byDirectory;
            }
        }

        if (string.IsNullOrWhiteSpace(upn))
        {
            return null;
        }

        string normalizedUpn = upn.Trim();
        User? byUpn = await db.Users
            .FirstOrDefaultAsync(candidate => candidate.Upn == normalizedUpn, cancellationToken);

        if (byUpn is not null)
        {
            if (allowJit
                && !string.IsNullOrWhiteSpace(externalId)
                && string.IsNullOrWhiteSpace(byUpn.DirectoryObjectId))
            {
                await TryBindDirectoryObjectIdAsync(byUpn, externalId, cancellationToken);
            }

            return byUpn;
        }

        if (!allowJit)
        {
            return null;
        }

        return await ProvisionGoogleUserAsync(normalizedUpn, displayName, externalId, cancellationToken);
    }

    private async Task TryBindDirectoryObjectIdAsync(
        User user,
        string externalId,
        CancellationToken cancellationToken)
    {
        bool directoryTaken = await db.Users
            .AsNoTracking()
            .AnyAsync(
                candidate => candidate.DirectoryObjectId == externalId && candidate.Id != user.Id,
                cancellationToken);
        if (directoryTaken)
        {
            return;
        }

        AdminAuditComposer.UserAuditState before = AdminAuditComposer.CaptureUser(user);
        user.BindDirectoryObjectId(externalId, clock.UtcNow);
        List<BusinessAuditEntry> changes = AdminAuditComposer.UserProfileChanges(before, user).ToList();
        if (changes.Count == 0)
        {
            return;
        }

        await sharedDbTransaction.ExecuteAsync(async ct =>
        {
            await businessAudit.AppendManyAsync(changes, ct);
        }, cancellationToken);
    }

    private async Task<User> ProvisionGoogleUserAsync(
        string upn,
        string? displayName,
        string? externalId,
        CancellationToken cancellationToken)
    {
        string name = string.IsNullOrWhiteSpace(displayName) ? upn : displayName.Trim();
        User user = User.Create(
            upn,
            name,
            UserType.Employee,
            clock.UtcNow,
            directoryObjectId: string.IsNullOrWhiteSpace(externalId) ? null : externalId);

        await sharedDbTransaction.ExecuteAsync(async ct =>
        {
            db.Users.Add(user);

            Role? employeeRole = await db.Roles
                .FirstOrDefaultAsync(role => role.Name == EmployeeRoleName, ct);
            if (employeeRole is not null)
            {
                db.UserRoles.Add(UserRole.Create(user.Id, employeeRole.Id, clock.UtcNow));
            }

            await businessAudit.AppendAsync(AdminAuditComposer.UserCreated(user), ct);
        }, cancellationToken);

        return user;
    }

    private async Task<CurrentUserDto> MapSessionAsync(
        User user,
        string authMethod,
        string? avatarUrl,
        CancellationToken cancellationToken)
    {
        List<CurrentUserRoleDto> roles = await (
            from userRole in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == user.Id
            orderby role.Name
            select new CurrentUserRoleDto(role.Id, role.Name))
            .ToListAsync(cancellationToken);

        bool isPlatformAdmin = roles.Any(role =>
            string.Equals(
                role.Name,
                IdentitySeedCatalog.PlatformAdministratorRoleName,
                StringComparison.Ordinal));

        // Platform Administrator always receives the full permission catalog in /me
        // so UI gates (can()) stay complete even when new permissions were just seeded.
        List<string> permissions = isPlatformAdmin
            ? await db.Permissions
                .AsNoTracking()
                .Select(permission => permission.Key)
                .Distinct()
                .OrderBy(key => key)
                .ToListAsync(cancellationToken)
            : await db.UserRoles
                .AsNoTracking()
                .Where(userRole => userRole.UserId == user.Id)
                .SelectMany(userRole => userRole.Role.RolePermissions)
                .Select(rolePermission => rolePermission.Permission.Key)
                .Distinct()
                .OrderBy(key => key)
                .ToListAsync(cancellationToken);

        return new CurrentUserDto(
            user.Id,
            user.Upn,
            user.DisplayName,
            user.UserType.ToString(),
            user.TimeZone,
            authMethod,
            roles,
            permissions,
            avatarUrl);
    }
}
