using Microsoft.AspNetCore.Builder;

namespace Qec.Itmg.Identity.Authorization;

public static class PermissionEndpointConventionBuilderExtensions
{
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permissionKey)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);
        string policyName = PermissionPolicyName.For(permissionKey);
        return builder.RequireAuthorization(policyName);
    }
}
