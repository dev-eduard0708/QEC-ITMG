using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb.Domain;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.Contracts.Numbering;

namespace Qec.Itmg.Cmdb.Services;

public sealed record AssetDto(
    Guid Id,
    string AssetNumber,
    Guid? ConfigurationItemId,
    string? ConfigurationItemNumber,
    string AssetType,
    string Name,
    string? SerialNumber,
    string? Manufacturer,
    string? Model,
    DateOnly? PurchaseDate,
    decimal? PurchaseCost,
    DateOnly? WarrantyExpiry,
    string Status,
    Guid? LocationId,
    string? Notes,
    Guid? ActiveAssignedToUserId,
    DateTimeOffset? ActiveAssignedAtUtc,
    string RowVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AssetAssignmentDto(
    Guid Id,
    Guid AssetId,
    Guid AssignedToUserId,
    Guid AssignedByUserId,
    DateTimeOffset AssignedAtUtc,
    DateTimeOffset? ReturnedAtUtc,
    string? Notes,
    bool IsActive);

public sealed class AssetService(
    CmdbDbContext db,
    INumberSequenceService numbers,
    IClock clock)
{
    public const string AssetSequenceKey = "assets";
    public const string AssetNumberPrefix = "AST";

    public async Task<IReadOnlyList<AssetDto>> ListAsync(
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Asset> query = db.Assets.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(item =>
                item.Name.Contains(term)
                || item.AssetNumber.Contains(term)
                || (item.SerialNumber != null && item.SerialNumber.Contains(term)));
        }

        List<Asset> assets = await query.OrderBy(item => item.Name).ToListAsync(cancellationToken);
        return await MapManyAsync(assets, cancellationToken);
    }

    public async Task<AssetDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Asset? asset = await db.Assets.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return asset is null ? null : (await MapManyAsync([asset], cancellationToken)).Single();
    }

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
        await EnsureCiExistsAsync(configurationItemId, cancellationToken);

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

    public async Task<Asset> UpdateAsync(
        Guid id,
        string assetType,
        string name,
        AssetStatus status,
        Guid? configurationItemId,
        string? serialNumber,
        string? manufacturer,
        string? model,
        DateOnly? purchaseDate,
        decimal? purchaseCost,
        DateOnly? warrantyExpiry,
        Guid? locationId,
        string? notes,
        string rowVersion,
        CancellationToken cancellationToken = default)
    {
        Asset entity = await db.Assets.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Asset was not found.");

        if (!MatchesRowVersion(entity.RowVersion, rowVersion))
        {
            throw new InvalidOperationException("The asset was modified by another user.");
        }

        await EnsureCiExistsAsync(configurationItemId, cancellationToken);

        entity.UpdateProfile(
            assetType,
            name,
            status,
            configurationItemId,
            serialNumber,
            manufacturer,
            model,
            purchaseDate,
            purchaseCost,
            warrantyExpiry,
            locationId,
            notes,
            clock.UtcNow);
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

    public async Task<IReadOnlyList<AssetAssignmentDto>> ListAssignmentsAsync(
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        return await db.AssetAssignments.AsNoTracking()
            .Where(item => item.AssetId == assetId)
            .OrderByDescending(item => item.AssignedAtUtc)
            .Select(item => new AssetAssignmentDto(
                item.Id,
                item.AssetId,
                item.AssignedToUserId,
                item.AssignedByUserId,
                item.AssignedAtUtc,
                item.ReturnedAtUtc,
                item.Notes,
                item.ReturnedAtUtc == null))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssetDto>> ListActiveEquipmentForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        List<Guid> assetIds = await db.AssetAssignments.AsNoTracking()
            .Where(item => item.AssignedToUserId == userId && item.ReturnedAtUtc == null)
            .Select(item => item.AssetId)
            .ToListAsync(cancellationToken);

        if (assetIds.Count == 0)
        {
            return [];
        }

        List<Asset> assets = await db.Assets.AsNoTracking()
            .Where(item => assetIds.Contains(item.Id))
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return await MapManyAsync(assets, cancellationToken);
    }

    private async Task EnsureCiExistsAsync(Guid? configurationItemId, CancellationToken cancellationToken)
    {
        if (configurationItemId is not Guid ciId || ciId == Guid.Empty)
        {
            return;
        }

        bool ciExists = await db.ConfigurationItems.AsNoTracking()
            .AnyAsync(item => item.Id == ciId, cancellationToken);
        if (!ciExists)
        {
            throw new InvalidOperationException("Linked configuration item was not found.");
        }
    }

    private async Task<IReadOnlyList<AssetDto>> MapManyAsync(
        IReadOnlyList<Asset> assets,
        CancellationToken cancellationToken)
    {
        if (assets.Count == 0)
        {
            return [];
        }

        Guid[] assetIds = assets.Select(item => item.Id).ToArray();
        Guid[] ciIds = assets
            .Where(item => item.ConfigurationItemId is not null)
            .Select(item => item.ConfigurationItemId!.Value)
            .Distinct()
            .ToArray();

        Dictionary<Guid, string> ciNumbers = ciIds.Length == 0
            ? []
            : await db.ConfigurationItems.AsNoTracking()
                .Where(item => ciIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.CiNumber, cancellationToken);

        Dictionary<Guid, (Guid UserId, DateTimeOffset AssignedAtUtc)> activeAssignments =
            await db.AssetAssignments.AsNoTracking()
                .Where(item => assetIds.Contains(item.AssetId) && item.ReturnedAtUtc == null)
                .ToDictionaryAsync(
                    item => item.AssetId,
                    item => (item.AssignedToUserId, item.AssignedAtUtc),
                    cancellationToken);

        return assets.Select(item =>
        {
            activeAssignments.TryGetValue(item.Id, out (Guid UserId, DateTimeOffset AssignedAtUtc) active);
            bool hasActive = activeAssignments.ContainsKey(item.Id);
            return new AssetDto(
                item.Id,
                item.AssetNumber,
                item.ConfigurationItemId,
                item.ConfigurationItemId is Guid ciId && ciNumbers.TryGetValue(ciId, out string? number)
                    ? number
                    : null,
                item.AssetType,
                item.Name,
                item.SerialNumber,
                item.Manufacturer,
                item.Model,
                item.PurchaseDate,
                item.PurchaseCost,
                item.WarrantyExpiry,
                item.Status.ToString(),
                item.LocationId,
                item.Notes,
                hasActive ? active.UserId : null,
                hasActive ? active.AssignedAtUtc : null,
                Convert.ToBase64String(item.RowVersion),
                item.CreatedAtUtc,
                item.UpdatedAtUtc);
        }).ToList();
    }

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
