using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Identity.Admin;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Identity.Seed;

namespace Qec.Itmg.Identity.Authentication;

public interface IDevelopmentQuickLoginService
{
    Task<User> EnsureAdministratorAsync(CancellationToken cancellationToken = default);

    Task<User> EnsureEmployeeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Provisions Development quick-login users on first use only (not via Identity seed config).
/// </summary>
public sealed class DevelopmentQuickLoginService(
    IdentityDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit) : IDevelopmentQuickLoginService
{
    public Task<User> EnsureAdministratorAsync(CancellationToken cancellationToken = default) =>
        EnsureUserWithRoleAsync(
            DevelopmentLoginPrincipalFactory.AdminUpn,
            DevelopmentLoginPrincipalFactory.AdminDisplayName,
            DevelopmentLoginPrincipalFactory.AdminExternalId,
            IdentitySeedCatalog.PlatformAdministratorRoleName,
            cancellationToken);

    public Task<User> EnsureEmployeeAsync(CancellationToken cancellationToken = default) =>
        EnsureUserWithRoleAsync(
            DevelopmentLoginPrincipalFactory.EmployeeUpn,
            DevelopmentLoginPrincipalFactory.EmployeeDisplayName,
            DevelopmentLoginPrincipalFactory.EmployeeExternalId,
            IdentitySeedCatalog.EmployeeRoleName,
            cancellationToken);

    private async Task<User> EnsureUserWithRoleAsync(
        string upn,
        string displayName,
        string externalId,
        string roleName,
        CancellationToken cancellationToken)
    {
        Role role = await db.Roles.FirstOrDefaultAsync(item => item.Name == roleName, cancellationToken)
            ?? throw new InvalidOperationException($"Required role '{roleName}' was not found. Run identity seed first.");

        User? user = await db.Users.FirstOrDefaultAsync(
            candidate => candidate.Upn == upn || candidate.DirectoryObjectId == externalId,
            cancellationToken);

        if (user is null)
        {
            user = User.Create(upn, displayName, UserType.Employee, clock.UtcNow, directoryObjectId: externalId);
            db.Users.Add(user);
            await businessAudit.AppendAsync(AdminAuditComposer.UserCreated(user), cancellationToken);
        }
        else
        {
            if (user.Status != UserStatus.Active)
            {
                user.Enable(clock.UtcNow);
            }

            if (string.IsNullOrWhiteSpace(user.DirectoryObjectId))
            {
                user.BindDirectoryObjectId(externalId, clock.UtcNow);
            }
        }

        bool hasRole = await db.UserRoles.AnyAsync(
            link => link.UserId == user.Id && link.RoleId == role.Id,
            cancellationToken);
        if (!hasRole)
        {
            db.UserRoles.Add(UserRole.Create(user.Id, role.Id, clock.UtcNow));
            await businessAudit.AppendAsync(
                AdminAuditComposer.RoleAssigned(user.Id, user.Upn, role.Id, role.Name),
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return user;
    }
}
