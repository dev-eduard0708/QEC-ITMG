using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Qec.Itmg.Identity.Domain;

namespace Qec.Itmg.Identity.Authorization;

/// <summary>
/// Builds authorization policies dynamically from permission keys (e.g. admin.users).
/// </summary>
public sealed class PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        AuthorizationPolicy? existing = await base.GetPolicyAsync(policyName);
        if (existing is not null)
        {
            return existing;
        }

        if (!PermissionPolicyName.TryCreate(policyName, out string? permissionKey) || permissionKey is null)
        {
            return null;
        }

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissionKey))
            .Build();
    }
}

public static class PermissionPolicyName
{
    public static string For(string permissionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);
        string normalized = permissionKey.Trim().ToLowerInvariant();
        Permission.EnsureValidKey(normalized);
        return normalized;
    }

    public static bool TryCreate(string policyName, out string? permissionKey)
    {
        permissionKey = null;
        if (string.IsNullOrWhiteSpace(policyName))
        {
            return false;
        }

        string candidate = policyName.Trim().ToLowerInvariant();
        try
        {
            Permission.EnsureValidKey(candidate);
        }
        catch (ArgumentException)
        {
            return false;
        }

        permissionKey = candidate;
        return true;
    }
}
