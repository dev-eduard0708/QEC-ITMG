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
        ("tickets.read", "View service desk tickets"),
        ("tickets.manage", "Manage service desk tickets, queues, and assignment"),
        ("incidents.security", "View and change incident security classification"),
        ("problems.read", "View problems"),
        ("problems.manage", "Manage problems and incident links"),
        ("change.create", "Create change requests"),
        ("change.read", "View change requests"),
        ("change.assess", "Assess change requests"),
        ("change.approve", "Approve or reject change requests"),
        ("change.schedule", "Schedule approved changes"),
        ("change.implement", "Implement and validate changes"),
        ("change.pir", "Complete post-implementation review"),
        ("change.catalog.manage", "Manage standard change catalog"),
        ("event.read", "View operational events"),
        ("event.acknowledge", "Acknowledge operational events"),
        ("event.promote", "Promote operational events to incidents"),
        ("event.admin", "Ingest and administer operational events"),
        ("kb.read", "View knowledge base articles in IT workspace"),
        ("kb.manage", "Manage knowledge base articles"),
    ];
}
