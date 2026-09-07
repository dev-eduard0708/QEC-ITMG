namespace Qec.Itmg.Identity.Authorization;

/// <summary>Succeeds when the user has any one of the listed permission keys.</summary>
public sealed class AnyPermissionRequirement : Microsoft.AspNetCore.Authorization.IAuthorizationRequirement
{
    public AnyPermissionRequirement(IReadOnlyList<string> permissionKeys)
    {
        ArgumentNullException.ThrowIfNull(permissionKeys);
        if (permissionKeys.Count == 0)
            throw new ArgumentException("At least one permission key is required.", nameof(permissionKeys));

        PermissionKeys = permissionKeys
            .Select(k =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(k);
                return k.Trim().ToLowerInvariant();
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<string> PermissionKeys { get; }
}
