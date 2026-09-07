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
/// Idempotent IT department + starter position hierarchy. Does not seed user assignments
/// and does not overwrite existing position names/parents.
/// </summary>
public sealed class OrganizationPositionsSeedRunner(
    OrganizationDbContext db,
    IClock clock,
    ILogger<OrganizationPositionsSeedRunner> logger) : IOrganizationPositionsSeedRunner
{
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
        Department? it = await db.Departments.FirstOrDefaultAsync(x => x.Name == "IT", cancellationToken);
        if (it is null)
        {
            it = Department.Create("IT", clock.UtcNow, "Information Technology");
            db.Departments.Add(it);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Created IT department for organization hierarchy seed.");
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
            "Organization positions seed completed for IT department ({Created} created, {Total} catalog).",
            created,
            Seeds.Length);
    }
}
