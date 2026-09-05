using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb.Domain;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.Contracts.Numbering;

namespace Qec.Itmg.Cmdb.Services;

public sealed record CiTypeDto(Guid Id, string Key, string Name, string? Description, bool IsActive);

public sealed record ConfigurationItemDto(
    Guid Id,
    string CiNumber,
    Guid CiTypeId,
    string CiTypeKey,
    string CiTypeName,
    string Name,
    string? Description,
    string Status,
    string? Criticality,
    Guid? LocationId,
    Guid? DepartmentId,
    Guid? OwnerUserId,
    string? SerialNumber,
    string? Manufacturer,
    string? Model,
    string? Notes,
    bool IsSinglePointOfFailure,
    string? SpofReason,
    DateTimeOffset? SpofReviewedAtUtc,
    string? SpofMitigationNotes,
    Guid? SpofRiskId,
    string RowVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CiRelationshipDto(
    Guid Id,
    Guid SourceCiId,
    Guid TargetCiId,
    string RelationshipType,
    string? Notes,
    DateTimeOffset CreatedAtUtc);

public sealed class ConfigurationItemService(
    CmdbDbContext db,
    INumberSequenceService numbers,
    IClock clock)
{
    public const string CiSequenceKey = "configuration-items";
    public const string CiNumberPrefix = "CI";

    public async Task<IReadOnlyList<CiTypeDto>> ListCiTypesAsync(CancellationToken cancellationToken = default)
    {
        return await db.CiTypes.AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new CiTypeDto(item.Id, item.Key, item.Name, item.Description, item.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<CiType> CreateCiTypeAsync(
        string key,
        string name,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        CiType entity = CiType.Create(key, name, clock.UtcNow, description);
        db.CiTypes.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<IReadOnlyList<ConfigurationItemDto>> ListConfigurationItemsAsync(
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ConfigurationItem> query = db.ConfigurationItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(item =>
                item.Name.Contains(term) || item.CiNumber.Contains(term));
        }

        List<ConfigurationItem> items = await query
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return await MapManyAsync(items, cancellationToken);
    }

    public async Task<ConfigurationItemDto?> GetConfigurationItemAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ConfigurationItem? item = await db.ConfigurationItems.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        return item is null ? null : (await MapManyAsync([item], cancellationToken)).Single();
    }

    public async Task<ConfigurationItem> CreateConfigurationItemAsync(
        Guid ciTypeId,
        string name,
        string? description = null,
        CiCriticality? criticality = null,
        Guid? locationId = null,
        Guid? departmentId = null,
        Guid? ownerUserId = null,
        string? serialNumber = null,
        string? manufacturer = null,
        string? model = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        bool typeExists = await db.CiTypes.AsNoTracking()
            .AnyAsync(item => item.Id == ciTypeId && item.IsActive, cancellationToken);
        if (!typeExists)
        {
            throw new InvalidOperationException("CI type was not found or is inactive.");
        }

        string ciNumber = await numbers.NextAsync(CiSequenceKey, CiNumberPrefix, cancellationToken);
        ConfigurationItem entity = ConfigurationItem.Create(
            ciNumber,
            ciTypeId,
            name,
            clock.UtcNow,
            description,
            criticality,
            locationId,
            departmentId,
            ownerUserId,
            serialNumber,
            manufacturer,
            model,
            notes);

        db.ConfigurationItems.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<ConfigurationItem> UpdateConfigurationItemAsync(
        Guid id,
        string name,
        string? description,
        ConfigurationItemStatus status,
        CiCriticality? criticality,
        Guid? locationId,
        Guid? departmentId,
        Guid? ownerUserId,
        string? serialNumber,
        string? manufacturer,
        string? model,
        string? notes,
        string rowVersion,
        CancellationToken cancellationToken = default)
    {
        ConfigurationItem entity = await db.ConfigurationItems.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Configuration item was not found.");

        if (!MatchesRowVersion(entity.RowVersion, rowVersion))
        {
            throw new InvalidOperationException("The configuration item was modified by another user.");
        }

        entity.UpdateProfile(
            name,
            description,
            status,
            criticality,
            locationId,
            departmentId,
            ownerUserId,
            serialNumber,
            manufacturer,
            model,
            notes,
            clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<IReadOnlyList<ConfigurationItemDto>> MapManyAsync(
        IReadOnlyList<ConfigurationItem> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return [];
        }

        Guid[] typeIds = items.Select(item => item.CiTypeId).Distinct().ToArray();
        Dictionary<Guid, CiType> types = await db.CiTypes.AsNoTracking()
            .Where(item => typeIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        return items.Select(item =>
        {
            types.TryGetValue(item.CiTypeId, out CiType? type);
            return new ConfigurationItemDto(
                item.Id,
                item.CiNumber,
                item.CiTypeId,
                type?.Key ?? string.Empty,
                type?.Name ?? string.Empty,
                item.Name,
                item.Description,
                item.Status.ToString(),
                item.Criticality?.ToString(),
                item.LocationId,
                item.DepartmentId,
                item.OwnerUserId,
                item.SerialNumber,
                item.Manufacturer,
                item.Model,
                item.Notes,
                item.IsSinglePointOfFailure,
                item.SpofReason,
                item.SpofReviewedAtUtc,
                item.SpofMitigationNotes,
                item.SpofRiskId,
                Convert.ToBase64String(item.RowVersion),
                item.CreatedAtUtc,
                item.UpdatedAtUtc);
        }).ToList();
    }

    public async Task<ConfigurationItemDto> SetSpofAsync(
        Guid id,
        bool isSinglePointOfFailure,
        string? reason,
        string? mitigationNotes,
        Guid? riskId,
        bool confirmed,
        string rowVersion,
        CancellationToken cancellationToken = default)
    {
        ConfigurationItem entity = await db.ConfigurationItems.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Configuration item was not found.");
        if (!MatchesRowVersion(entity.RowVersion, rowVersion))
            throw new InvalidOperationException("The configuration item was modified by another user.");
        entity.SetSinglePointOfFailure(isSinglePointOfFailure, reason, mitigationNotes, riskId, clock.UtcNow, confirmed);
        await db.SaveChangesAsync(cancellationToken);
        return (await MapManyAsync([entity], cancellationToken)).Single();
    }

    public async Task<IReadOnlyList<ConfigurationItemDto>> ListSpofsAsync(CancellationToken cancellationToken = default)
    {
        List<ConfigurationItem> items = await db.ConfigurationItems.AsNoTracking()
            .Where(x => x.IsSinglePointOfFailure)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return await MapManyAsync(items, cancellationToken);
    }

    public async Task<int> CountConfirmedSpofsAsync(CancellationToken cancellationToken = default) =>
        await db.ConfigurationItems.AsNoTracking().CountAsync(x => x.IsSinglePointOfFailure, cancellationToken);

    private static bool MatchesRowVersion(byte[] current, string expectedBase64)
    {
        if (string.IsNullOrWhiteSpace(expectedBase64))
        {
            return current.Length == 0;
        }

        try
        {
            byte[] expected = Convert.FromBase64String(expectedBase64.Trim());
            return current.AsSpan().SequenceEqual(expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
