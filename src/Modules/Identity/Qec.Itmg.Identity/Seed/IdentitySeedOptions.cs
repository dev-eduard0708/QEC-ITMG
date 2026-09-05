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
    public const string AuditorRoleName = "Auditor";

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
        ("ops.read", "View IT operations records (backups, certificates, patches, jobs)"),
        ("ops.manage", "Manage IT operations job metadata"),
        ("backup.manage", "Manage backup jobs, runs, and restore tests"),
        ("cert.manage", "Manage certificate records (no private keys)"),
        ("patch.manage", "Manage patch baselines and deployment tracking"),
        ("access.request", "Create and view access cases (JML / access requests)"),
        ("access.approve", "Approve or reject access cases"),
        ("access.fulfill", "Fulfill and verify access case checklist items"),
        ("access.review", "Manage access review campaigns and decisions"),
        ("access.privileged.manage", "Manage privileged and service account metadata"),
        ("sod.manage", "Manage segregation of duties rules and exceptions"),
        ("doc.read", "View managed documents"),
        ("doc.manage", "Create and manage managed documents and versions"),
        ("doc.approve", "Approve managed documents"),
        ("policy.read", "View policies"),
        ("policy.manage", "Create and manage policies"),
        ("policy.approve", "Approve policies"),
        ("policy.acknowledge", "Acknowledge published policies"),
        ("gov.read", "View governance workspace, organization chart, and registers"),
        ("gov.manage", "Manage organization chart and governance profile"),
        ("control.read", "View internal controls and test procedures"),
        ("control.manage", "Create and manage internal controls, links, and evidence requirements"),
        ("framework.manage", "Manage compliance frameworks, versions, and requirement content"),
        ("compliance.read", "View frameworks, mappings, coverage, and compliance calendar"),
        ("assessment.perform", "Perform and record control assessments"),
        ("evidence.read", "View evidence metadata and authorized attachments"),
        ("evidence.upload", "Create and upload evidence drafts and versions"),
        ("evidence.accept", "Accept, return, or withdraw evidence"),
        ("evidence.export", "Privileged audited evidence export"),
        ("audit.read", "View audit engagements, questions, findings, and CAPA"),
        ("audit.manage", "Create and manage audit engagements, questions, and evidence requests"),
        ("finding.manage", "Manage findings, management responses, and corrective actions"),
        ("kb.read", "View knowledge base articles in IT workspace"),
        ("kb.manage", "Manage knowledge base articles"),
        ("sec.dashboard", "View security dashboard counts"),
        ("vuln.read", "View vulnerabilities"),
        ("vuln.manage", "Create and manage vulnerabilities and remediation links"),
        ("risk.manage", "Manage risk register"),
        ("exception.approve", "Approve or reject security/policy exceptions"),
        ("ticket.read.security", "View security classification on tickets/incidents"),
        ("bcm.read", "View business continuity BIA, plans, procedures, DR tests, and reports"),
        ("bcm.manage", "Manage BIA, continuity plans, procedures, and SPOF metadata"),
        ("dr.test.manage", "Schedule, run, and complete DR tests and evidence links"),
        ("vendor.read", "View vendors, contracts, assessments, and vendor access summaries"),
        ("vendor.manage", "Manage vendors, contacts, and CI/access vendor links"),
        ("contract.manage", "Manage vendor contracts and contractual SLA references"),
        ("vendor.assess", "Schedule and complete vendor assessments"),
    ];

    /// <summary>Read-oriented auditor role. Does not include manage/export/admin permissions.</summary>
    public static readonly string[] AuditorPermissionKeys =
    [
        "audit.read",
        "evidence.read",
        "control.read",
        "compliance.read",
    ];
}
