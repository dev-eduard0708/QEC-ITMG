namespace Qec.Itmg.Identity.Seed;

public sealed class IdentitySeedOptions
{
    public const string SectionName = "Identity:Seed";

    /// <summary>
    /// Optional Google Workspace email/UPN for the first Platform Administrator.
    /// Empty by default — set via environment or local config (never commit a real mailbox).
    /// </summary>
    public string? PlatformAdministratorUpn { get; set; }
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
    ];
}
