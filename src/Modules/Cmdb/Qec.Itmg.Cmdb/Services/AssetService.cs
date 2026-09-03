using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb.Domain;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.Contracts.Numbering;

namespace Qec.Itmg.Cmdb.Services;

public sealed class AssetService(
    CmdbDbContext db,
    INumberSequenceService numbers,
    IClock clock)
{
    public const string AssetSequenceKey = "assets";
    public const string AssetNumberPrefix = "AST";

    public async Task<Asset> CreateAsync(
        string assetType,
        string name,
        Guid? configurationItemId = null,
        string? serialNumber = null,
        string? manufacturer = null,
        string? model = null,
        DateOnly? purchaseDate = null,
        decimal? purchaseCost = null,
        DateOnly? warrantyExpiry = null,
        Guid? locationId = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        if (configurationItemId is Guid ciId && ciId != Guid.Empty)
        {
            bool ciExists = await db.ConfigurationItems.AsNoTracking()
                .AnyAsync(item => item.Id == ciId, cancellationToken);
            if (!ciExists)
            {
                throw new InvalidOperationException("Linked configuration item was not found.");
            }
        }

        string assetNumber = await numbers.NextAsync(AssetSequenceKey, AssetNumberPrefix, cancellationToken);
        Asset entity = Asset.Create(
            assetNumber,
            assetType,
            name,
            clock.UtcNow,
            configurationItemId,
            serialNumber,
            manufacturer,
            model,
            purchaseDate,
            purchaseCost,
            warrantyExpiry,
            locationId,
            notes);

        db.Assets.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<AssetAssignment> AssignAsync(
        Guid assetId,
        Guid assignedToUserId,
        Guid assignedByUserId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        Asset asset = await db.Assets.FirstOrDefaultAsync(item => item.Id == assetId, cancellationToken)
            ?? throw new InvalidOperationException("Asset was not found.");

        bool hasActive = await db.AssetAssignments.AsNoTracking()
            .AnyAsync(item => item.AssetId == assetId && item.ReturnedAtUtc == null, cancellationToken);
        if (hasActive)
        {
            throw new InvalidOperationException("Asset already has an active assignment.");
        }

        DateTimeOffset utcNow = clock.UtcNow;
        AssetAssignment assignment = AssetAssignment.Create(
            assetId,
            assignedToUserId,
            assignedByUserId,
            utcNow,
            notes);
        asset.MarkAssigned(utcNow);

        db.AssetAssignments.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);
        return assignment;
    }

    public async Task<AssetAssignment> ReturnAsync(
        Guid assetId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        Asset asset = await db.Assets.FirstOrDefaultAsync(item => item.Id == assetId, cancellationToken)
            ?? throw new InvalidOperationException("Asset was not found.");

        AssetAssignment assignment = await db.AssetAssignments
            .FirstOrDefaultAsync(item => item.AssetId == assetId && item.ReturnedAtUtc == null, cancellationToken)
            ?? throw new InvalidOperationException("Asset has no active assignment.");

        DateTimeOffset utcNow = clock.UtcNow;
        assignment.Return(utcNow, notes);
        asset.MarkInStock(utcNow);
        await db.SaveChangesAsync(cancellationToken);
        return assignment;
    }
}
