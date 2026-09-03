namespace Qec.Itmg.Identity.Seed;

public sealed class IdentitySeedOptions
{
    public const string SectionName = "Identity:Seed";

    /// <summary>
    /// Optional Google Workspace email/UPN for the first Platform Administrator.
    /// Empty by default — set via environment or local config (never commit a real mailbox).
    /// </summary>
    public string? PlatformAdministratorUpn { get; set; }

    /// <summary>
    /// Optional display name for the bootstrap Platform Administrator (development convenience).
    /// </summary>
    public string? PlatformAdministratorDisplayName { get; set; }
}

public static class IdentitySeedCatalog
{
    public const string EmployeeRoleName = "Employee";
    public const string PlatformAdministratorRoleName = "Platform Administrator";

    public static readonly (string Key, string Description)[] SystemPermissions =
    [
        ("admin.users", "Manage users and user-role assignments"),
        ("admin.roles", "Manage roles and role-permission assignments"),
        ("admin.settings", "Manage platform settings"),
        ("admin.integrations", "Manage integrations"),
        ("admin.lookups", "Manage organization lookups"),
        ("cmdb.read", "View configuration items and CMDB data"),
        ("cmdb.manage", "Manage configuration items and CMDB data"),
        ("assets.read", "View assets"),
        ("assets.manage", "Manage assets"),
    ];
}
