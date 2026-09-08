using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Organization.Domain;
using Qec.Itmg.Organization.Persistence;

namespace Qec.Itmg.Host.Organization;

public interface IOrganizationPositionsSeedRunner
{
    Task RunAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Idempotent HR-aligned organizational units + position hierarchies.
/// Does not seed user assignments. Conservatively aligns known starter nodes;
/// never overwrites admin-customized names/parents outside known starter patterns.
/// </summary>
public sealed class OrganizationPositionsSeedRunner(
    OrganizationDbContext db,
    IClock clock,
    ILogger<OrganizationPositionsSeedRunner> logger) : IOrganizationPositionsSeedRunner
{
    private sealed record DeptSeed(
        string Code,
        string NameEn,
        string? NameAr,
        string? DescriptionEn,
        string? DescriptionAr,
        int SortOrder,
        string? ParentCode,
        DepartmentUnitType UnitType,
        string[] KnownOldNames);

    private sealed record PositionSeed(
        string DepartmentCode,
        string Key,
        string NameEn,
        string NameAr,
        string? ParentKey,
        bool Managerial,
        int SortOrder,
        string? DescriptionEn,
        string? DescriptionAr);

    private static readonly DeptSeed[] Departments =
    [
        new(
            "QEC",
            "Quality Education Company",
            "شركة جودة التعليم",
            "QEC Head Office company root.",
            "جذر شركة جودة التعليم للمكتب الرئيسي.",
            0,
            null,
            DepartmentUnitType.Company,
            []),
        new(
            "EXEC",
            "Executive Management",
            "الإدارة التنفيذية",
            "Corporate executive leadership and executive office support.",
            "القيادة التنفيذية للشركة ودعم المكتب التنفيذي.",
            10,
            "QEC",
            DepartmentUnitType.Department,
            ["Executive"]),
        new(
            "HR",
            "Human Resources",
            "الموارد البشرية",
            "Human Resources",
            null,
            20,
            "QEC",
            DepartmentUnitType.Department,
            ["Human Resources"]),
        new(
            "HR-PAYROLL",
            "Payroll",
            "الرواتب",
            null,
            null,
            21,
            "HR",
            DepartmentUnitType.Section,
            []),
        new(
            "HR-ADMIN",
            "Admin & Support",
            "الإدارة والدعم",
            null,
            null,
            22,
            "HR",
            DepartmentUnitType.Section,
            []),
        new(
            "HR-GOVREL",
            "Government Relations",
            "العلاقات الحكومية",
            null,
            null,
            23,
            "HR",
            DepartmentUnitType.Section,
            []),
        new(
            "HR-PERSONNEL",
            "Personnel Administration",
            "إدارة شؤون الموظفين",
            null,
            null,
            24,
            "HR",
            DepartmentUnitType.Section,
            []),
        new(
            "HR-COMPBEN",
            "Compensation and Benefits",
            "التعويضات والمزايا",
            null,
            null,
            25,
            "HR",
            DepartmentUnitType.Section,
            []),
        new(
            "HR-OPS",
            "HR Operations",
            "عمليات الموارد البشرية",
            null,
            null,
            26,
            "HR",
            DepartmentUnitType.Section,
            []),
        new(
            "HR-RECRUIT",
            "Recruitment",
            "التوظيف",
            null,
            null,
            27,
            "HR",
            DepartmentUnitType.Section,
            []),
        new(
            "FINANCE",
            "Finance",
            "المالية",
            "Finance",
            null,
            30,
            "QEC",
            DepartmentUnitType.Department,
            ["Finance"]),
        new(
            "IT",
            "Information Technology",
            "تقنية المعلومات",
            "Information Technology",
            null,
            40,
            "QEC",
            DepartmentUnitType.Department,
            ["IT"]),
        new(
            "LEGAL",
            "Legal Affairs",
            "الشؤون القانونية",
            null,
            null,
            50,
            "QEC",
            DepartmentUnitType.Department,
            []),
        new(
            "PM",
            "PMO",
            "مكتب إدارة المشاريع",
            "Identifies project opportunities, coordinates project delivery and staffing requirements, selects or recommends project personnel, and endorses selected candidates to Human Resources for formal employment and onboarding processing.",
            "تتولى إدارة المشاريع تحديد فرص المشاريع وتنسيق تنفيذها واحتياجاتها من القوى العاملة واختيار أو ترشيح الكوادر المطلوبة للمشاريع وإحالة المرشحين المختارين إلى الموارد البشرية لاستكمال إجراءات التوظيف والانضمام الرسمية.",
            60,
            "QEC",
            DepartmentUnitType.Department,
            ["Project Management", "Project"]),
        new(
            "PM-STAFF",
            "PM Staff",
            "فريق إدارة المشاريع",
            null,
            null,
            61,
            "PM",
            DepartmentUnitType.Team,
            []),
        new(
            "PM-OPS",
            "Operations and International Coordination",
            "العمليات والتنسيق الدولي",
            null,
            null,
            62,
            "PM",
            DepartmentUnitType.Section,
            []),
        new(
            "PM-PM",
            "PM",
            "إدارة المشاريع",
            null,
            null,
            63,
            "PM",
            DepartmentUnitType.Section,
            []),
        new(
            "PM-OFFICE",
            "PM Office",
            "مكتب المشاريع",
            null,
            null,
            64,
            "PM",
            DepartmentUnitType.Office,
            []),
        new(
            "MARKETING",
            "Marketing",
            "التسويق",
            null,
            null,
            70,
            "QEC",
            DepartmentUnitType.Department,
            []),
        new(
            "CYBER-GOV",
            "Cybersecurity & IT Governance Committee",
            "لجنة الأمن السيبراني وحوكمة تقنية المعلومات",
            "Cross-functional QEC Head Office committee for cybersecurity oversight, IT governance, security awareness, technology risk, control coordination, audit readiness, and follow-up of findings and corrective actions.",
            "لجنة مشتركة بين الإدارات في المكتب الرئيسي لشركة جودة التعليم للإشراف على الأمن السيبراني وحوكمة تقنية المعلومات والتوعية الأمنية ومخاطر التقنية وتنسيق الضوابط والجاهزية للتدقيق ومتابعة الملاحظات والإجراءات التصحيحية.",
            80,
            "QEC",
            DepartmentUnitType.Committee,
            []),
    ];

    private static readonly HashSet<string> StarterCodesUnderQec =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "EXEC", "HR", "FINANCE", "IT", "PM", "LEGAL", "MARKETING", "CYBER-GOV",
        };

    /// <summary>
    /// Vacant starter-only legacy keys soft-deactivated after HR-aligned positions are ensured.
    /// Never deleted; never deactivated when PositionAssignments exist.
    /// </summary>
    private static readonly string[] LegacyStarterKeysToDeactivateWhenVacant =
    [
        "EXEC-VP",
        "EXEC-VP-SECRETARY",
        "EXEC-CEO-SECRETARY",
        "IT_DIRECTOR",
        "IT_MANAGER",
        "IT_ADMINISTRATOR",
        "SYSTEMS_ADMINISTRATOR",
        "NETWORK_ADMINISTRATOR",
        "IT_SUPPORT_SPECIALIST",
        "IT_SECURITY_GOVERNANCE",
        "HR_MANAGER",
        "PM-HEAD",
        "PM-PROJECT-MANAGER",
        "PM-PROJECT-COORDINATOR",
        "PM-STAFFING-COORDINATOR",
        "PM-DEVELOPMENT-COORDINATOR",
    ];

    private static readonly PositionSeed[] Positions =
    [
        // Executive Management — HR Portal baseline
        new(
            "EXEC", "EXEC-CEO", "CEO", "الرئيس التنفيذي", null, true, 10,
            "Leads QEC at the corporate level and provides overall executive direction.",
            "يقود QEC على المستوى المؤسسي ويوفر التوجيه التنفيذي العام."),
        new(
            "EXEC", "EXEC-EXECUTIVE-VP", "Executive Vice President", "نائب الرئيس التنفيذي",
            "EXEC-CEO", true, 20,
            "Supports the Chief Executive Officer and provides executive leadership.",
            "يدعم الرئيس التنفيذي ويوفر القيادة التنفيذية."),
        new(
            "EXEC", "EXEC-EXECUTIVE-SECRETARY", "Executive Secretary", "السكرتير التنفيذي",
            "EXEC-CEO", false, 30,
            "Provides executive office support.",
            "يقدم الدعم للمكتب التنفيذي."),
        new(
            "EXEC", "EXEC-OFFICE-MANAGER", "Office Manager", "مدير المكتب",
            "EXEC-CEO", false, 40,
            "Manages executive office operations.",
            "يدير عمليات المكتب التنفيذي."),

        // Finance
        new(
            "FINANCE", "FIN-GENERAL-MANAGER", "General Finance Manager", "المدير العام للمالية",
            null, true, 10, null, null),
        new(
            "FINANCE", "FIN-ACCOUNTING-MANAGER", "Accounting Manager", "مدير المحاسبة",
            "FIN-GENERAL-MANAGER", true, 20, null, null),
        new(
            "FINANCE", "FIN-ACCOUNTING-SUPERVISOR", "Accounting Supervisor", "مشرف المحاسبة",
            "FIN-ACCOUNTING-MANAGER", true, 30, null, null),
        new(
            "FINANCE", "FIN-ACCOUNTANT", "Accountant", "محاسب",
            "FIN-ACCOUNTING-SUPERVISOR", false, 40, null, null),

        // Legal Affairs
        new(
            "LEGAL", "LEGAL-GENERAL-MANAGER", "General Legal Affairs Manager", "المدير العام للشؤون القانونية",
            null, true, 10, null, null),
        new(
            "LEGAL", "LEGAL-RESEARCHER", "Legal Researcher", "باحث قانوني",
            "LEGAL-GENERAL-MANAGER", false, 20, null, null),

        // Information Technology — HR Portal baseline (new codes; vacant legacy soft-deactivated)
        new(
            "IT", "IT-GENERAL-MANAGER", "General IT Manager", "المدير العام لتقنية المعلومات",
            null, true, 10, null, null),
        new(
            "IT", "IT-SYSTEMS-ADMIN", "Systems Admin", "مسؤول الأنظمة",
            "IT-GENERAL-MANAGER", false, 20, null, null),
        new(
            "IT", "IT-NETWORK-TECHNICIAN", "Network Technician", "فني شبكات",
            "IT-GENERAL-MANAGER", false, 30, null, null),
        new(
            "IT", "IT-COMPUTER-ENGINEER", "Computer Engineer", "مهندس حاسب آلي",
            "IT-GENERAL-MANAGER", false, 40, null, null),
        new(
            "IT", "IT-PROGRAMMER", "Programmer", "مبرمج",
            "IT-GENERAL-MANAGER", false, 50, null, null),

        // Human Resources
        new(
            "HR", "HR-DEPUTY-GENERAL-MANAGER", "Deputy General HR Manager", "نائب المدير العام للموارد البشرية",
            null, true, 10, null, null),
        new(
            "HR", "HR-SPECIALIST", "HR Specialist", "أخصائي موارد بشرية",
            "HR-DEPUTY-GENERAL-MANAGER", false, 20, null, null),

        // Payroll (HR-PAYROLL → HR_PAYROLL)
        new(
            "HR-PAYROLL", "HR-PAYROLL-OPERATIONS-MANAGER", "HR Operations Manager", "مدير عمليات الموارد البشرية",
            null, true, 10, null, null),
        new(
            "HR-PAYROLL", "HR-PAYROLL-SPECIALIST", "HR Specialist", "أخصائي موارد بشرية",
            null, false, 20, null, null),
        new(
            "HR-PAYROLL", "HR-PAYROLL-SENIOR-SPECIALIST", "Senior HR Specialist", "أخصائي موارد بشرية أول",
            null, false, 30, null, null),

        // Government Relations
        new(
            "HR-GOVREL", "HR-GOV-REL-MANAGER", "Government Relations Manager", "مدير العلاقات الحكومية",
            null, true, 10, null, null),
        new(
            "HR-GOVREL", "HR-GOV-REL-SPECIALIST", "Government Relations Specialist", "أخصائي علاقات حكومية",
            "HR-GOV-REL-MANAGER", false, 20, null, null),
        new(
            "HR-GOVREL", "HR-GOV-REL-SENIOR-HR-SPECIALIST", "Senior HR Specialist", "أخصائي موارد بشرية أول",
            "HR-GOV-REL-MANAGER", false, 30, null, null),

        // Personnel Administration
        new(
            "HR-PERSONNEL", "HR-PERSONNEL-COORDINATOR", "HR Coordinator", "منسق موارد بشرية",
            null, false, 10, null, null),

        // Admin & Support
        new(
            "HR-ADMIN", "HR-ADMIN-SUPPORT-CLEANER", "Cleaner", "عامل نظافة",
            null, false, 10, null, null),
        new(
            "HR-ADMIN", "HR-ADMIN-SUPPORT-DRIVER", "Driver", "سائق",
            null, false, 20, null, null),
        new(
            "HR-ADMIN", "HR-ADMIN-SUPPORT-LABOR", "Labor", "عامل",
            null, false, 30, null, null),
        new(
            "HR-ADMIN", "HR-ADMIN-SUPPORT-LOGISTIC-SUPERVISOR", "Logistic Support Supervisor", "مشرف الدعم اللوجستي",
            null, true, 40, null, null),
        new(
            "HR-ADMIN", "HR-ADMIN-SUPPORT-LOGISTIC-SPECIALIST", "Logistic Support Specialist", "أخصائي دعم لوجستي",
            "HR-ADMIN-SUPPORT-LOGISTIC-SUPERVISOR", false, 50, null, null),
        new(
            "HR-ADMIN", "HR-ADMIN-SUPPORT-TEABOY", "Teaboy", "عامل ضيافة",
            null, false, 60, null, null),

        // PMO
        new(
            "PM", "PM-GENERAL-PROJECT-MANAGER", "General Project Manager", "المدير العام للمشاريع",
            null, true, 10, null, null),
        new(
            "PM", "PM-DEPUTY-MANAGER", "PM Deputy Manager", "نائب مدير إدارة المشاريع",
            "PM-GENERAL-PROJECT-MANAGER", true, 20, null, null),
        new(
            "PM", "PM-FIRST-PROGRAMS-MANAGER", "First Programs Manager", "مدير البرامج الأول",
            "PM-DEPUTY-MANAGER", true, 30, null, null),
        new(
            "PM", "PM-ACADEMIC-SUPERVISOR", "Academic Supervisor", "مشرف أكاديمي",
            "PM-DEPUTY-MANAGER", false, 40, null, null),
        new(
            "PM", "PM-EXECUTIVE-SECRETARY", "Executive Secretary", "سكرتير تنفيذي",
            "PM-DEPUTY-MANAGER", false, 50, null, null),

        new(
            "PM-OFFICE", "PM-OFFICE-ADMINISTRATIVE", "Administrative", "إداري",
            null, false, 10, null, null),

        new(
            "PM-STAFF", "PM-STAFF-PROJECT-COORDINATOR", "Project Coordinator", "منسق مشاريع",
            null, false, 10, null, null),
        new(
            "PM-STAFF", "PM-STAFF-TALENT-ACQUISITION-SPECIALIST", "Talent Acquisition Specialist",
            "أخصائي استقطاب المواهب", null, false, 20, null, null),

        // Cybersecurity & IT Governance Committee — vacant only; never seed assignments
        new(
            "CYBER-GOV", "CYBER-GOV-CHAIR", "Committee Chair / Executive Sponsor",
            "رئيس اللجنة / الراعي التنفيذي", null, true, 10, null, null),
        new(
            "CYBER-GOV", "CYBER-GOV-LEAD", "Cybersecurity & IT Governance Lead",
            "مسؤول قيادة الأمن السيبراني وحوكمة تقنية المعلومات", "CYBER-GOV-CHAIR", true, 20, null, null),
        new(
            "CYBER-GOV", "CYBER-GOV-COORDINATOR", "Security & Governance Coordinator",
            "منسق الأمن والحوكمة", "CYBER-GOV-LEAD", false, 30, null, null),
        new(
            "CYBER-GOV", "CYBER-GOV-PEOPLE", "People Security & Awareness Representative",
            "ممثل أمن الأفراد والتوعية الأمنية", "CYBER-GOV-LEAD", false, 40, null, null),
        new(
            "CYBER-GOV", "CYBER-GOV-FINANCE", "Financial Systems & Controls Representative",
            "ممثل الأنظمة والضوابط المالية", "CYBER-GOV-LEAD", false, 50, null, null),
        new(
            "CYBER-GOV", "CYBER-GOV-TECH-SME", "Technical Security Member / SME",
            "عضو الأمن التقني / خبير متخصص", "CYBER-GOV-LEAD", false, 60, null, null),
    ];

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Parents before children (null parent first, then by SortOrder).
        foreach (DeptSeed dept in Departments
                     .OrderBy(d => d.ParentCode is null ? 0 : 1)
                     .ThenBy(d => d.SortOrder))
        {
            await EnsureDepartmentAsync(dept, cancellationToken);
        }

        await AttachOrphanStarterDepartmentsToQecAsync(cancellationToken);
        await AlignStarterParentsUnderQecAsync(cancellationToken);

        Dictionary<string, Guid> departmentIds = await db.Departments.AsNoTracking()
            .Where(d => d.Code != null)
            .ToDictionaryAsync(d => d.Code!, d => d.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        int created = 0;
        foreach (IGrouping<string, PositionSeed> group in Positions.GroupBy(p => p.DepartmentCode))
        {
            string deptCode = Department.NormalizeCode(group.Key);
            if (!departmentIds.TryGetValue(deptCode, out Guid departmentId))
            {
                logger.LogWarning("Skipping positions for missing department {Code}.", deptCode);
                continue;
            }

            created += await EnsurePositionsAsync(departmentId, group.ToArray(), cancellationToken);
        }

        await ReconcileLegacyStarterPositionsAsync(cancellationToken);

        logger.LogInformation(
            "Organization hierarchy seed completed ({Created} positions created across starter departments).",
            created);
    }

    private async Task ReconcileLegacyStarterPositionsAsync(CancellationToken ct)
    {
        foreach (string key in LegacyStarterKeysToDeactivateWhenVacant)
        {
            string normalized = Position.NormalizeKey(key);
            List<Position> matches = await db.Positions
                .Where(p => p.Key == normalized && p.IsActive)
                .ToListAsync(ct);

            foreach (Position position in matches)
            {
                bool hasAssignments = await db.PositionAssignments
                    .AnyAsync(a => a.PositionId == position.Id && a.EffectiveTo == null, ct);
                if (hasAssignments)
                {
                    logger.LogInformation(
                        "Preserving occupied legacy position {Key} (assignments present).",
                        normalized);
                    continue;
                }

                position.Deactivate(clock.UtcNow);
                await db.SaveChangesAsync(ct);
                logger.LogInformation(
                    "Soft-deactivated vacant legacy starter position {Key}.",
                    normalized);
            }
        }
    }

    private async Task AttachOrphanStarterDepartmentsToQecAsync(CancellationToken ct)
    {
        Department? qec = await db.Departments.FirstOrDefaultAsync(x => x.Code == "QEC", ct);
        if (qec is null)
        {
            return;
        }

        string[] attachCodes = ["EXEC", "PM", "HR", "FINANCE", "IT", "LEGAL", "MARKETING", "CYBER-GOV"];
        foreach (string code in attachCodes)
        {
            string normalized = Department.NormalizeCode(code);
            Department? dept = await db.Departments.FirstOrDefaultAsync(x => x.Code == normalized, ct);
            if (dept is null || dept.ParentDepartmentId is not null)
            {
                continue;
            }

            dept.SetParent(qec.Id, clock.UtcNow);
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Attached orphan starter department {Code} under QEC.",
                normalized);
        }
    }

    private async Task AlignStarterParentsUnderQecAsync(CancellationToken ct)
    {
        Department? qec = await db.Departments.AsNoTracking().FirstOrDefaultAsync(x => x.Code == "QEC", ct);
        Department? exec = await db.Departments.AsNoTracking().FirstOrDefaultAsync(x => x.Code == "EXEC", ct);
        if (qec is null)
        {
            return;
        }

        foreach (string code in StarterCodesUnderQec)
        {
            if (string.Equals(code, "EXEC", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            DeptSeed? seed = Departments.FirstOrDefault(d =>
                string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase));
            if (seed is null)
            {
                continue;
            }

            string normalized = Department.NormalizeCode(code);
            Department? dept = await db.Departments.FirstOrDefaultAsync(x => x.Code == normalized, ct);
            if (dept is null)
            {
                continue;
            }

            bool underExec = exec is not null && dept.ParentDepartmentId == exec.Id;
            bool orphan = dept.ParentDepartmentId is null;
            bool knownName = seed.KnownOldNames.Any(n =>
                string.Equals(n, dept.Name, StringComparison.Ordinal))
                || string.Equals(dept.Name, seed.NameEn, StringComparison.Ordinal);

            // Reparent to QEC only for known starter pattern (orphan or still under EXEC with known name).
            if ((orphan || underExec) && knownName && dept.ParentDepartmentId != qec.Id)
            {
                dept.SetParent(qec.Id, clock.UtcNow);
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Reparented starter department {Code} under QEC.", code);
            }
        }
    }

    private async Task<int> EnsurePositionsAsync(
        Guid departmentId,
        PositionSeed[] seeds,
        CancellationToken cancellationToken)
    {
        Dictionary<string, Guid> byKey = (await db.Positions
            .Where(x => x.DepartmentId == departmentId)
            .Select(x => new { x.Key, x.Id })
            .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Key, x => x.Id, StringComparer.OrdinalIgnoreCase);

        int created = 0;
        foreach (PositionSeed seed in seeds)
        {
            string normalized = Position.NormalizeKey(seed.Key);
            if (byKey.ContainsKey(normalized))
            {
                continue;
            }

            Guid? parentId = null;
            if (seed.ParentKey is not null)
            {
                string parentNormalized = Position.NormalizeKey(seed.ParentKey);
                if (!byKey.TryGetValue(parentNormalized, out Guid resolvedParent))
                {
                    logger.LogWarning(
                        "Skipping position {Key}: parent {ParentKey} not found yet.",
                        normalized,
                        parentNormalized);
                    continue;
                }

                parentId = resolvedParent;
            }

            Position position = Position.Create(
                departmentId,
                normalized,
                seed.NameEn,
                seed.NameAr,
                clock.UtcNow,
                parentId,
                seed.DescriptionEn,
                seed.DescriptionAr,
                isManagerial: seed.Managerial,
                sortOrder: seed.SortOrder,
                isActive: true);
            db.Positions.Add(position);
            await db.SaveChangesAsync(cancellationToken);
            byKey[normalized] = position.Id;
            created++;
        }

        return created;
    }

    private async Task EnsureDepartmentAsync(DeptSeed seed, CancellationToken ct)
    {
        string normalized = Department.NormalizeCode(seed.Code);
        Guid? parentId = null;
        if (seed.ParentCode is not null)
        {
            Department? parent = await db.Departments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == Department.NormalizeCode(seed.ParentCode), ct);
            parentId = parent?.Id;
        }

        Department? existing = await db.Departments
            .FirstOrDefaultAsync(x => x.Code == normalized, ct);

        // Avoid matching by display name alone — codes are the stable identity; never duplicate codes.
        if (existing is null)
        {
            // Name uniqueness: if another row already uses this English name under a different code, skip create.
            bool nameTaken = await db.Departments.AnyAsync(x => x.Name == seed.NameEn, ct);
            if (nameTaken)
            {
                logger.LogWarning(
                    "Skipping create for department {Code}: name '{Name}' already used by another unit.",
                    normalized,
                    seed.NameEn);
                return;
            }

            db.Departments.Add(Department.Create(
                seed.NameEn,
                clock.UtcNow,
                seed.DescriptionEn,
                normalized,
                seed.NameAr,
                seed.DescriptionAr,
                parentId,
                seed.SortOrder,
                seed.UnitType));
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Created department {Code} ({Name}).", normalized, seed.NameEn);
            return;
        }

        await AlignExistingDepartmentAsync(existing, seed, parentId, ct);
    }

    private async Task AlignExistingDepartmentAsync(
        Department existing,
        DeptSeed seed,
        Guid? desiredParentId,
        CancellationToken ct)
    {
        bool changed = false;
        bool isKnownOldName = seed.KnownOldNames.Any(n =>
            string.Equals(n, existing.Name, StringComparison.Ordinal));
        bool alreadyTargetName = string.Equals(existing.Name, seed.NameEn, StringComparison.Ordinal);

        // Safe rename only when still on a known old starter name.
        string nameEn = existing.Name;
        string? nameAr = existing.NameAr;
        if (isKnownOldName && !alreadyTargetName)
        {
            nameEn = seed.NameEn;
            nameAr = existing.NameAr ?? seed.NameAr;
            changed = true;
        }
        else if (alreadyTargetName && existing.NameAr is null && seed.NameAr is not null)
        {
            nameAr = seed.NameAr;
            changed = true;
        }

        // UnitType: set when aligning known starter nodes (old name or already target name).
        // Also always correct the QEC company root type if still the SQL/migration default.
        DepartmentUnitType unitType = existing.UnitType;
        if ((isKnownOldName || alreadyTargetName || string.Equals(seed.Code, "QEC", StringComparison.OrdinalIgnoreCase))
            && existing.UnitType != seed.UnitType)
        {
            unitType = seed.UnitType;
            changed = true;
        }

        // Parent: only when still on known starter pattern.
        Guid? parentId = existing.ParentDepartmentId;
        Department? exec = null;
        if (desiredParentId is Guid desired)
        {
            bool parentIsNull = existing.ParentDepartmentId is null;
            bool parentIsExec = false;
            if (StarterCodesUnderQec.Contains(seed.Code)
                && !string.Equals(seed.Code, "EXEC", StringComparison.OrdinalIgnoreCase))
            {
                exec = await db.Departments.AsNoTracking().FirstOrDefaultAsync(x => x.Code == "EXEC", ct);
                parentIsExec = exec is not null && existing.ParentDepartmentId == exec.Id;
            }

            bool safeToReparent = (isKnownOldName || alreadyTargetName)
                && (parentIsNull || parentIsExec || existing.ParentDepartmentId == desired);

            // EXEC special: parent null (old root) → QEC; do not overwrite other custom parents.
            if (string.Equals(seed.Code, "EXEC", StringComparison.OrdinalIgnoreCase))
            {
                safeToReparent = (isKnownOldName || alreadyTargetName || parentIsNull)
                    && (parentIsNull || existing.ParentDepartmentId == desired);
            }

            if (safeToReparent && existing.ParentDepartmentId != desired)
            {
                parentId = desired;
                changed = true;
            }
        }

        string? description = existing.Description ?? seed.DescriptionEn;
        string? descriptionAr = existing.DescriptionAr ?? seed.DescriptionAr;
        if ((existing.Description is null && seed.DescriptionEn is not null)
            || (existing.DescriptionAr is null && seed.DescriptionAr is not null))
        {
            changed = true;
        }

        int sortOrder = existing.SortOrder == 0 ? seed.SortOrder : existing.SortOrder;
        if (existing.SortOrder == 0 && seed.SortOrder != 0)
        {
            changed = true;
        }

        // One-time: restore inactive PM when still on a known starter name (children may already exist).
        bool isActive = existing.IsActive;
        if (!existing.IsActive
            && string.Equals(seed.Code, "PM", StringComparison.OrdinalIgnoreCase)
            && (isKnownOldName || alreadyTargetName))
        {
            isActive = true;
            changed = true;
        }

        if (!changed && existing.Code == Department.NormalizeCode(seed.Code))
        {
            return;
        }

        existing.UpdateDetails(
            nameEn,
            nameAr,
            Department.NormalizeCode(seed.Code),
            description,
            descriptionAr,
            parentId,
            sortOrder,
            isActive,
            clock.UtcNow,
            unitType);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Aligned starter department {Code}.", seed.Code);
    }
}
