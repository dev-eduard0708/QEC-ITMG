namespace Qec.Itmg.Cmdb.Domain;

public enum AssetStatus
{
    InStock = 0,
    Assigned = 1,
    InRepair = 2,
    Retired = 3,
    Disposed = 4,
}

public sealed class Asset
{
    private Asset()
    {
    }

    public Guid Id { get; private set; }

    public string AssetNumber { get; private set; } = null!;

    public Guid? ConfigurationItemId { get; private set; }

    public string AssetType { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? SerialNumber { get; private set; }

    public string? Manufacturer { get; private set; }

    public string? Model { get; private set; }

    public DateOnly? PurchaseDate { get; private set; }

    public decimal? PurchaseCost { get; private set; }

    public DateOnly? WarrantyExpiry { get; private set; }

    public AssetStatus Status { get; private set; }

    public Guid? LocationId { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static Asset Create(
        string assetNumber,
        string assetType,
        string name,
        DateTimeOffset utcNow,
        Guid? configurationItemId = null,
        string? serialNumber = null,
        string? manufacturer = null,
        string? model = null,
        DateOnly? purchaseDate = null,
        decimal? purchaseCost = null,
        DateOnly? warrantyExpiry = null,
        Guid? locationId = null,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Asset
        {
            Id = Guid.CreateVersion7(),
            AssetNumber = assetNumber.Trim(),
            ConfigurationItemId = configurationItemId is null || configurationItemId == Guid.Empty
                ? null
                : configurationItemId,
            AssetType = assetType.Trim(),
            Name = name.Trim(),
            SerialNumber = NormalizeOptional(serialNumber),
            Manufacturer = NormalizeOptional(manufacturer),
            Model = NormalizeOptional(model),
            PurchaseDate = purchaseDate,
            PurchaseCost = purchaseCost,
            WarrantyExpiry = warrantyExpiry,
            Status = AssetStatus.InStock,
            LocationId = locationId is null || locationId == Guid.Empty ? null : locationId,
            Notes = NormalizeOptional(notes),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void MarkAssigned(DateTimeOffset utcNow)
    {
        Status = AssetStatus.Assigned;
        UpdatedAtUtc = utcNow;
    }

    public void MarkInStock(DateTimeOffset utcNow)
    {
        Status = AssetStatus.InStock;
        UpdatedAtUtc = utcNow;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
