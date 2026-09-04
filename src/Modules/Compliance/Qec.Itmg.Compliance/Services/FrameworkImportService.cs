using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Compliance.Domain;
using Qec.Itmg.Compliance.Persistence;

namespace Qec.Itmg.Compliance.Services;

public sealed record FrameworkImportPayload(
    string FrameworkCode,
    string FrameworkName,
    string Publisher,
    string? Description,
    string VersionCode,
    string? VersionTitle,
    bool SetCurrent,
    IReadOnlyList<FrameworkImportRequirement> Requirements);

public sealed record FrameworkImportRequirement(
    string Code,
    string Title,
    string RequirementType,
    string? ParentCode,
    string? Text,
    int? SortOrder);

public sealed record FrameworkImportResult(Guid FrameworkId, Guid VersionId, int RequirementsCreated, int RequirementsSkipped);

public sealed class FrameworkImportService(ComplianceDbContext db, IClock clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<FrameworkImportResult> ImportJsonAsync(string json, CancellationToken ct)
    {
        FrameworkImportPayload? payload = JsonSerializer.Deserialize<FrameworkImportPayload>(json, JsonOptions)
            ?? throw new ArgumentException("Invalid import JSON.");
        return await ImportAsync(payload, ct);
    }

    public async Task<FrameworkImportResult> ImportAsync(FrameworkImportPayload payload, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.FrameworkCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.VersionCode);

        Framework? framework = await db.Frameworks
            .FirstOrDefaultAsync(x => x.Code == payload.FrameworkCode.Trim().ToUpperInvariant(), ct);
        if (framework is null)
        {
            framework = Framework.Create(
                payload.FrameworkCode, payload.FrameworkName, payload.Publisher, clock.UtcNow, payload.Description);
            db.Frameworks.Add(framework);
            await db.SaveChangesAsync(ct);
        }

        FrameworkVersion? version = await db.FrameworkVersions
            .FirstOrDefaultAsync(x => x.FrameworkId == framework.Id && x.VersionCode == payload.VersionCode.Trim(), ct);
        if (version is null)
        {
            if (payload.SetCurrent)
            {
                List<FrameworkVersion> currents = await db.FrameworkVersions
                    .Where(x => x.FrameworkId == framework.Id && x.IsCurrent).ToListAsync(ct);
                foreach (FrameworkVersion c in currents) c.SetCurrent(false);
            }

            version = FrameworkVersion.Create(
                framework.Id, payload.VersionCode, clock.UtcNow, payload.VersionTitle, isCurrent: payload.SetCurrent);
            db.FrameworkVersions.Add(version);
            await db.SaveChangesAsync(ct);
        }

        Dictionary<string, Guid> codeToId = await db.FrameworkRequirements
            .Where(x => x.FrameworkVersionId == version.Id)
            .ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase, ct);

        // Pass 1: create without parents; Pass 2: set parents for newly created — simpler: topological by parent presence
        int created = 0, skipped = 0;
        List<FrameworkImportRequirement> remaining = payload.Requirements.ToList();
        int guard = remaining.Count + 5;
        while (remaining.Count > 0 && guard-- > 0)
        {
            List<FrameworkImportRequirement> progress = [];
            foreach (FrameworkImportRequirement row in remaining)
            {
                if (codeToId.ContainsKey(row.Code))
                {
                    skipped++;
                    progress.Add(row);
                    continue;
                }

                Guid? parentId = null;
                if (!string.IsNullOrWhiteSpace(row.ParentCode))
                {
                    if (!codeToId.TryGetValue(row.ParentCode.Trim(), out Guid pid))
                        continue; // wait for parent
                    parentId = pid;
                }

                if (!Enum.TryParse(row.RequirementType, true, out FrameworkRequirementType type))
                    type = FrameworkRequirementType.Other;

                FrameworkRequirement entity = FrameworkRequirement.Create(
                    version.Id, row.Code, row.Title, type, parentId, row.Text, row.SortOrder);
                db.FrameworkRequirements.Add(entity);
                await db.SaveChangesAsync(ct);
                codeToId[entity.Code] = entity.Id;
                created++;
                progress.Add(row);
            }

            remaining = remaining.Except(progress).ToList();
            if (progress.Count == 0) break;
        }

        if (remaining.Count > 0)
            throw new InvalidOperationException(
                $"Could not import {remaining.Count} requirements (missing parents or cycles): {string.Join(", ", remaining.Select(x => x.Code))}");

        return new(framework.Id, version.Id, created, skipped);
    }
}
