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
/// Idempotent starter departments + IT position hierarchy. Does not seed user assignments
/// and does not overwrite existing position names/parents.
/// </summary>
public sealed class OrganizationPositionsSeedRunner(
    OrganizationDbContext db,
    IClock clock,
    ILogger<OrganizationPositionsSeedRunner> logger) : IOrganizationPositionsSeedRunner
{
    private static readonly (string Code, string NameEn, string? NameAr, string? Description, int SortOrder)[] Departments =
    [
        ("IT", "IT", "تقنية المعلومات", "Information Technology", 10),
        ("FINANCE", "Finance", "المالية", "Finance", 20),
        ("HR", "Human Resources", "الموارد البشرية", "Human Resources", 30),
    ];

    private static readonly (string Key, string NameEn, string NameAr, string? ParentKey, bool Managerial, int SortOrder)[] Seeds =
    [
        ("IT_DIRECTOR", "IT Director", "مدير تقنية المعلومات", null, true, 10),
        ("IT_MANAGER", "IT Manager", "مدير تقنية المعلومات التشغيلي", "IT_DIRECTOR", true, 20),
        ("IT_ADMINISTRATOR", "IT Administrator", "مسؤول تقنية المعلومات", "IT_MANAGER", false, 30),
        ("SYSTEMS_ADMINISTRATOR", "Systems Administrator", "مسؤول الأنظمة", "IT_MANAGER", false, 40),
        ("NETWORK_ADMINISTRATOR", "Network Administrator", "مسؤول الشبكات", "IT_MANAGER", false, 50),
        ("IT_SUPPORT_SPECIALIST", "IT Support Specialist", "أخصائي دعم تقنية المعلومات", "IT_MANAGER", false, 60),
        ("IT_SECURITY_GOVERNANCE", "IT Security & Governance Officer", "مسؤول أمن وحوكمة تقنية المعلومات", "IT_MANAGER", false, 70),
    ];

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        foreach ((string code, string nameEn, string? nameAr, string? description, int sortOrder) in Departments)
        {
            await EnsureDepartmentAsync(code, nameEn, nameAr, description, sortOrder, cancellationToken);
        }

        Department it = await db.Departments.FirstAsync(x => x.Code == "IT" || x.Name == "IT", cancellationToken);
        if (string.IsNullOrWhiteSpace(it.Code) || it.Code != "IT")
        {
            it.UpdateDetails(it.Name, it.NameAr, "IT", it.Description, it.DescriptionAr, it.ParentDepartmentId, it.SortOrder, it.IsActive, clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }

        Dictionary<string, Guid> byKey = (await db.Positions
            .Where(x => x.DepartmentId == it.Id)
            .Select(x => new { x.Key, x.Id })
            .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Key, x => x.Id, StringComparer.OrdinalIgnoreCase);

        int created = 0;
        foreach ((string key, string nameEn, string nameAr, string? parentKey, bool managerial, int sortOrder) in Seeds)
        {
            string normalized = Position.NormalizeKey(key);
            if (byKey.ContainsKey(normalized))
            {
                continue;
            }

            Guid? parentId = null;
            if (parentKey is not null)
            {
                string parentNormalized = Position.NormalizeKey(parentKey);
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
                it.Id,
                normalized,
                nameEn,
                nameAr,
                clock.UtcNow,
                parentId,
                isManagerial: managerial,
                sortOrder: sortOrder,
                isActive: true);
            db.Positions.Add(position);
            await db.SaveChangesAsync(cancellationToken);
            byKey[normalized] = position.Id;
            created++;
        }

        logger.LogInformation(
            "Organization hierarchy seed completed ({Created} IT positions created, {Total} catalog; departments ensured).",
            created,
            Seeds.Length);
    }

    private async Task EnsureDepartmentAsync(
        string code,
        string nameEn,
        string? nameAr,
        string? description,
        int sortOrder,
        CancellationToken ct)
    {
        string normalized = Department.NormalizeCode(code);
        Department? existing = await db.Departments
            .FirstOrDefaultAsync(x => x.Code == normalized || x.Name == nameEn, ct);
        if (existing is null)
        {
            db.Departments.Add(Department.Create(
                nameEn,
                clock.UtcNow,
                description,
                normalized,
                nameAr,
                sortOrder: sortOrder));
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Created department {Code} ({Name}).", normalized, nameEn);
            return;
        }

        // Backfill code / bilingual fields without renaming if already present.
        if (string.IsNullOrWhiteSpace(existing.Code)
            || existing.Code != normalized
            || (existing.NameAr is null && nameAr is not null)
            || existing.SortOrder == 0)
        {
            existing.UpdateDetails(
                existing.Name,
                existing.NameAr ?? nameAr,
                normalized,
                existing.Description ?? description,
                existing.DescriptionAr,
                existing.ParentDepartmentId,
                existing.SortOrder == 0 ? sortOrder : existing.SortOrder,
                existing.IsActive,
                clock.UtcNow);
            await db.SaveChangesAsync(ct);
        }
    }
}
