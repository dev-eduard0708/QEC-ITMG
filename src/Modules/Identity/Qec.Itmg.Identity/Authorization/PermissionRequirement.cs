namespace Qec.Itmg.Identity.Authorization;

public sealed class PermissionRequirement : Microsoft.AspNetCore.Authorization.IAuthorizationRequirement
{
    public PermissionRequirement(string permissionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);
        PermissionKey = permissionKey.Trim().ToLowerInvariant();
    }

    public string PermissionKey { get; }
}
