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
/// Idempotent starter departments + position hierarchies.
/// Does not seed user assignments and does not overwrite existing position names/parents
/// or manually configured department parents.
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
        string? ParentCode);

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
            "EXEC",
            "Executive",
            "الإدارة التنفيذية",
            "Corporate executive leadership and executive office support.",
            "القيادة التنفيذية للشركة ودعم المكتب التنفيذي.",
            1,
            null),
        new(
            "PM",
            "Project Management",
            "إدارة المشاريع",
            "Identifies project opportunities, coordinates project delivery and staffing requirements, selects or recommends project personnel, and endorses selected candidates to Human Resources for formal employment and onboarding processing.",
            "تتولى إدارة المشاريع تحديد فرص المشاريع وتنسيق تنفيذها واحتياجاتها من القوى العاملة واختيار أو ترشيح الكوادر المطلوبة للمشاريع وإحالة المرشحين المختارين إلى الموارد البشرية لاستكمال إجراءات التوظيف والانضمام الرسمية.",
            10,
            "EXEC"),
        new(
            "HR",
            "Human Resources",
            "الموارد البشرية",
            "Human Resources",
            null,
            20,
            "EXEC"),
        new(
            "FINANCE",
            "Finance",
            "المالية",
            "Finance",
            null,
            30,
            "EXEC"),
        new(
            "IT",
            "IT",
            "تقنية المعلومات",
            "Information Technology",
            null,
            40,
            "EXEC"),
    ];

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
        // Ensure Executive first so child parents resolve.
        foreach (DeptSeed dept in Departments.OrderBy(d => d.ParentCode is null ? 0 : 1).ThenBy(d => d.SortOrder))
        {
            await EnsureDepartmentAsync(dept, cancellationToken);
        }

        await AttachOrphanStarterDepartmentsToExecutiveAsync(cancellationToken);

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

    private async Task AttachOrphanStarterDepartmentsToExecutiveAsync(CancellationToken ct)
    {
        Department? exec = await db.Departments.FirstOrDefaultAsync(x => x.Code == "EXEC", ct);
        if (exec is null)
        {
            return;
        }

        string[] attachCodes = ["PM", "HR", "FINANCE", "IT"];
        foreach (string code in attachCodes)
        {
            Department? dept = await db.Departments.FirstOrDefaultAsync(x => x.Code == code, ct);
            if (dept is null || dept.ParentDepartmentId is not null)
            {
                continue;
            }

            dept.SetParent(exec.Id, clock.UtcNow);
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Attached orphan starter department {Code} under Executive.",
                code);
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
            .FirstOrDefaultAsync(x => x.Code == normalized || x.Name == seed.NameEn, ct);
        if (existing is null)
        {
            db.Departments.Add(Department.Create(
                seed.NameEn,
                clock.UtcNow,
                seed.DescriptionEn,
                normalized,
                seed.NameAr,
                seed.DescriptionAr,
                parentId,
                seed.SortOrder));
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Created department {Code} ({Name}).", normalized, seed.NameEn);
            return;
        }

        // Backfill code / bilingual / description without overwriting an existing parent.
        bool needsBackfill = string.IsNullOrWhiteSpace(existing.Code)
            || existing.Code != normalized
            || (existing.NameAr is null && seed.NameAr is not null)
            || (existing.Description is null && seed.DescriptionEn is not null)
            || (existing.DescriptionAr is null && seed.DescriptionAr is not null)
            || existing.SortOrder == 0;

        if (needsBackfill)
        {
            existing.UpdateDetails(
                existing.Name,
                existing.NameAr ?? seed.NameAr,
                normalized,
                existing.Description ?? seed.DescriptionEn,
                existing.DescriptionAr ?? seed.DescriptionAr,
                existing.ParentDepartmentId,
                existing.SortOrder == 0 ? seed.SortOrder : existing.SortOrder,
                existing.IsActive,
                clock.UtcNow);
            await db.SaveChangesAsync(ct);
        }

        // Attach starter PM to Executive only when it still has no parent.
        if (normalized == "PM"
            && existing.ParentDepartmentId is null
            && parentId is Guid execId)
        {
            existing.SetParent(execId, clock.UtcNow);
            await db.SaveChangesAsync(ct);
        }
    }
}
