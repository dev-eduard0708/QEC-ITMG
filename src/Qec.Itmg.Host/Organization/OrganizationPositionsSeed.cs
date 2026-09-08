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
    ];

    private static readonly HashSet<string> StarterCodesUnderQec =
        new(StringComparer.OrdinalIgnoreCase) { "EXEC", "HR", "FINANCE", "IT", "PM", "LEGAL", "MARKETING" };

    private static readonly PositionSeed[] Positions =
    [
        new(
            "EXEC", "EXEC-CEO", "Chief Executive Officer", "الرئيس التنفيذي", null, true, 10,
            "Leads QEC at the corporate level and provides overall executive direction.",
            "يقود QEC على المستوى المؤسسي ويوفر التوجيه التنفيذي العام."),
        new(
            "EXEC", "EXEC-CEO-SECRETARY", "CEO Executive Secretary", "السكرتير التنفيذي للرئيس التنفيذي",
            "EXEC-CEO", false, 20,
            "Provides executive office support to the Chief Executive Officer.",
            "يقدم الدعم للمكتب التنفيذي للرئيس التنفيذي."),
        new(
            "EXEC", "EXEC-VP", "Vice President", "نائب الرئيس", "EXEC-CEO", true, 30,
            "Supports the Chief Executive Officer and provides executive leadership.",
            "يدعم الرئيس التنفيذي ويوفر القيادة التنفيذية."),
        new(
            "EXEC", "EXEC-VP-SECRETARY", "VP Executive Secretary", "السكرتير التنفيذي لنائب الرئيس",
            "EXEC-VP", false, 40,
            "Provides executive office support to the Vice President.",
            "يقدم الدعم للمكتب التنفيذي لنائب الرئيس."),

        new(
            "HR", "HR_MANAGER", "HR Manager", "مدير الموارد البشرية", null, true, 10,
            "Leads Human Resources for QEC Head Office.",
            "يقود الموارد البشرية لمكتب QEC الرئيسي."),

        new(
            "PM", "PM-HEAD", "Head of Project Management", "مدير إدارة المشاريع", null, true, 10,
            "Leads the Project Management department and oversees project opportunities, delivery, staffing needs and coordination with other QEC departments.",
            "يقود إدارة المشاريع ويشرف على فرص المشاريع والتنفيذ واحتياجات القوى العاملة والتنسيق مع إدارات QEC الأخرى."),
        new(
            "PM", "PM-PROJECT-MANAGER", "Project Manager", "مدير مشروع", "PM-HEAD", true, 20,
            "Responsible for assigned projects, project delivery, requirements and coordination.",
            "مسؤول عن المشاريع المكلفة وتنفيذها ومتطلباتها والتنسيق الخاص بها."),
        new(
            "PM", "PM-PROJECT-COORDINATOR", "Project Coordinator", "منسق مشروع", "PM-PROJECT-MANAGER", false, 30,
            "Supports project administration, schedules, documentation and operational coordination.",
            "يدعم إدارة المشاريع والجداول الزمنية والتوثيق والتنسيق التشغيلي."),
        new(
            "PM", "PM-STAFFING-COORDINATOR", "Project Staffing Coordinator", "منسق القوى العاملة للمشاريع",
            "PM-PROJECT-MANAGER", false, 40,
            "Coordinates project staffing requirements and candidate selection/endorsement to Human Resources. This role does not replace HR's formal employment/onboarding authority.",
            "ينسق احتياجات القوى العاملة للمشاريع وترشيح المرشحين إلى الموارد البشرية. لا يحل هذا الدور محل صلاحية الموارد البشرية في التوظيف والانضمام الرسمي."),
        new(
            "PM", "PM-DEVELOPMENT-COORDINATOR", "Project Development Coordinator", "منسق تطوير المشاريع",
            "PM-HEAD", false, 50,
            "Supports identification, tracking and preparation of new project opportunities.",
            "يدعم تحديد فرص المشاريع الجديدة ومتابعتها وتجهيزها."),

        new("IT", "IT_DIRECTOR", "IT Director", "مدير تقنية المعلومات", null, true, 10, null, null),
        new("IT", "IT_MANAGER", "IT Manager", "مدير تقنية المعلومات التشغيلي", "IT_DIRECTOR", true, 20, null, null),
        new("IT", "IT_ADMINISTRATOR", "IT Administrator", "مسؤول تقنية المعلومات", "IT_MANAGER", false, 30, null, null),
        new("IT", "SYSTEMS_ADMINISTRATOR", "Systems Administrator", "مسؤول الأنظمة", "IT_MANAGER", false, 40, null, null),
        new("IT", "NETWORK_ADMINISTRATOR", "Network Administrator", "مسؤول الشبكات", "IT_MANAGER", false, 50, null, null),
        new("IT", "IT_SUPPORT_SPECIALIST", "IT Support Specialist", "أخصائي دعم تقنية المعلومات", "IT_MANAGER", false, 60, null, null),
        new("IT", "IT_SECURITY_GOVERNANCE", "IT Security & Governance Officer", "مسؤول أمن وحوكمة تقنية المعلومات", "IT_MANAGER", false, 70, null, null),
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
            .ToDictionaryAsync(d => d.Code, d => d.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        int created = 0;
        foreach (IGrouping<string, PositionSeed> group in Positions.GroupBy(p => p.DepartmentCode))
        {
            if (!departmentIds.TryGetValue(group.Key, out Guid departmentId))
            {
                logger.LogWarning("Skipping positions for missing department {Code}.", group.Key);
                continue;
            }

            created += await EnsurePositionsAsync(departmentId, group.ToArray(), cancellationToken);
        }

        logger.LogInformation(
            "Organization hierarchy seed completed ({Created} positions created across starter departments).",
            created);
    }

    private async Task AttachOrphanStarterDepartmentsToQecAsync(CancellationToken ct)
    {
        Department? qec = await db.Departments.FirstOrDefaultAsync(x => x.Code == "QEC", ct);
        if (qec is null)
        {
            return;
        }

        string[] attachCodes = ["EXEC", "PM", "HR", "FINANCE", "IT", "LEGAL", "MARKETING"];
        foreach (string code in attachCodes)
        {
            Department? dept = await db.Departments.FirstOrDefaultAsync(x => x.Code == code, ct);
            if (dept is null || dept.ParentDepartmentId is not null)
            {
                continue;
            }

            dept.SetParent(qec.Id, clock.UtcNow);
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Attached orphan starter department {Code} under QEC.",
                code);
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

            Department? dept = await db.Departments.FirstOrDefaultAsync(x => x.Code == code, ct);
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
