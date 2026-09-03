using Microsoft.AspNetCore.Authorization;

namespace Qec.Itmg.Identity.Authorization;

public sealed class PermissionAuthorizationHandler(IUserPermissionEvaluator permissionEvaluator)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        bool allowed = await permissionEvaluator.HasPermissionAsync(
            context.User,
            requirement.PermissionKey);

        if (allowed)
        {
            context.Succeed(requirement);
        }
    }
}
