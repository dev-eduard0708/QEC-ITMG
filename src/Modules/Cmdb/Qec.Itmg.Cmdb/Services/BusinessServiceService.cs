using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb.Domain;
using Qec.Itmg.Cmdb.Persistence;

namespace Qec.Itmg.Cmdb.Services;

public sealed class BusinessServiceService(CmdbDbContext db, IClock clock)
{
    public async Task<BusinessService> CreateAsync(
        string name,
        CiCriticality criticality,
        string? description = null,
        Guid? ownerUserId = null,
        int? rtoMinutes = null,
        int? rpoMinutes = null,
        CancellationToken cancellationToken = default)
    {
        BusinessService entity = BusinessService.Create(
            name,
            criticality,
            clock.UtcNow,
            description,
            ownerUserId,
            rtoMinutes,
            rpoMinutes);
        db.BusinessServices.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task LinkConfigurationItemAsync(
        Guid businessServiceId,
        Guid configurationItemId,
        CancellationToken cancellationToken = default)
    {
        bool serviceExists = await db.BusinessServices.AsNoTracking()
            .AnyAsync(item => item.Id == businessServiceId, cancellationToken);
        bool ciExists = await db.ConfigurationItems.AsNoTracking()
            .AnyAsync(item => item.Id == configurationItemId, cancellationToken);
        if (!serviceExists || !ciExists)
        {
            throw new InvalidOperationException("Business service or configuration item was not found.");
        }

        bool alreadyLinked = await db.BusinessServiceConfigurationItems.AsNoTracking()
            .AnyAsync(
                item => item.BusinessServiceId == businessServiceId
                    && item.ConfigurationItemId == configurationItemId,
                cancellationToken);
        if (alreadyLinked)
        {
            return;
        }

        db.BusinessServiceConfigurationItems.Add(
            BusinessServiceConfigurationItem.Create(businessServiceId, configurationItemId, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
    }
}
