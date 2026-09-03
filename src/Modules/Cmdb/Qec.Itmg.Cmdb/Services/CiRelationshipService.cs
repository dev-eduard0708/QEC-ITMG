using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb.Domain;
using Qec.Itmg.Cmdb.Persistence;

namespace Qec.Itmg.Cmdb.Services;

public sealed class CiRelationshipService(CmdbDbContext db, IClock clock)
{
    public async Task<CiRelationship> CreateAsync(
        Guid sourceCiId,
        Guid targetCiId,
        CiRelationshipType relationshipType,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        if (sourceCiId == targetCiId)
        {
            throw new InvalidOperationException("A configuration item cannot link to itself.");
        }

        bool sourceExists = await db.ConfigurationItems.AsNoTracking()
            .AnyAsync(item => item.Id == sourceCiId, cancellationToken);
        bool targetExists = await db.ConfigurationItems.AsNoTracking()
            .AnyAsync(item => item.Id == targetCiId, cancellationToken);
        if (!sourceExists || !targetExists)
        {
            throw new InvalidOperationException("Source or target configuration item was not found.");
        }

        CiRelationship entity = CiRelationship.Create(
            sourceCiId,
            targetCiId,
            relationshipType,
            clock.UtcNow,
            notes);
        db.CiRelationships.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }
}
