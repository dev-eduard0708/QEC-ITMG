using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Qec.Itmg.Identity.Audit;

namespace Qec.Itmg.Identity.Authorization;

public sealed class PermissionAuthorizationHandler(
    IUserPermissionEvaluator permissionEvaluator,
    IHttpContextAccessor httpContextAccessor)
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
            return;
        }

        await SecurityAuditHooks.LogPermissionDeniedAsync(
            httpContextAccessor.HttpContext,
            requirement.PermissionKey);
    }
}
