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

    /// <summary>Requires the caller to hold at least one of the given permission keys.</summary>
    public static TBuilder RequireAnyPermission<TBuilder>(this TBuilder builder, params string[] permissionKeys)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(permissionKeys);
        if (permissionKeys.Length == 0)
            throw new ArgumentException("At least one permission key is required.", nameof(permissionKeys));

        string policyName = AnyPermissionPolicyName.For(permissionKeys);
        return builder.RequireAuthorization(policyName);
    }
}
