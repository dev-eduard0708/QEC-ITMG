using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb.Domain;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.Contracts.Numbering;

namespace Qec.Itmg.Cmdb.Services;

public sealed class ConfigurationItemService(
    CmdbDbContext db,
    INumberSequenceService numbers,
    IClock clock)
{
    public const string CiSequenceKey = "configuration-items";
    public const string CiNumberPrefix = "CI";

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
}
