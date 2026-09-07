using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Qec.Itmg.Identity.Audit;

namespace Qec.Itmg.Identity.Authorization;

public sealed class AnyPermissionAuthorizationHandler(
    IUserPermissionEvaluator permissionEvaluator,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<AnyPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AnyPermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        foreach (string permissionKey in requirement.PermissionKeys)
        {
            if (await permissionEvaluator.HasPermissionAsync(context.User, permissionKey))
            {
                context.Succeed(requirement);
                return;
            }
        }

        await SecurityAuditHooks.LogPermissionDeniedAsync(
            httpContextAccessor.HttpContext,
            string.Join('|', requirement.PermissionKeys));
    }
}
