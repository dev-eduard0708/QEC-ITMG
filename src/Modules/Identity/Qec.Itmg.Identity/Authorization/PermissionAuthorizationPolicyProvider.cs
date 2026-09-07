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

        if (AnyPermissionPolicyName.TryCreate(policyName, out IReadOnlyList<string>? anyKeys)
            && anyKeys is { Count: > 0 })
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new AnyPermissionRequirement(anyKeys))
                .Build();
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

/// <summary>Policy names of the form <c>permission.any:key1|key2</c>.</summary>
public static class AnyPermissionPolicyName
{
    public const string Prefix = "permission.any:";

    public static string For(IEnumerable<string> permissionKeys)
    {
        ArgumentNullException.ThrowIfNull(permissionKeys);
        List<string> normalized = [];
        foreach (string key in permissionKeys)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            string n = key.Trim().ToLowerInvariant();
            Permission.EnsureValidKey(n);
            if (!normalized.Contains(n, StringComparer.Ordinal))
                normalized.Add(n);
        }

        if (normalized.Count == 0)
            throw new ArgumentException("At least one permission key is required.", nameof(permissionKeys));

        return Prefix + string.Join('|', normalized);
    }

    public static bool TryCreate(string policyName, out IReadOnlyList<string>? permissionKeys)
    {
        permissionKeys = null;
        if (string.IsNullOrWhiteSpace(policyName)
            || !policyName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string body = policyName[Prefix.Length..];
        if (string.IsNullOrWhiteSpace(body))
            return false;

        List<string> keys = [];
        foreach (string part in body.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate = part.ToLowerInvariant();
            try
            {
                Permission.EnsureValidKey(candidate);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (!keys.Contains(candidate, StringComparer.Ordinal))
                keys.Add(candidate);
        }

        if (keys.Count == 0)
            return false;

        permissionKeys = keys;
        return true;
    }
}
