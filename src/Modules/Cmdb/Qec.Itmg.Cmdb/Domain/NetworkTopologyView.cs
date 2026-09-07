namespace Qec.Itmg.Cmdb.Domain;

public sealed class NetworkTopologyView
{
    private NetworkTopologyView()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public Guid? LocationId { get; private set; }

    public string? Description { get; private set; }

    public bool IsDefault { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static NetworkTopologyView Create(
        string name,
        DateTimeOffset utcNow,
        Guid? locationId = null,
        string? description = null,
        bool isDefault = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new NetworkTopologyView
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            LocationId = NormalizeGuid(locationId),
            Description = NormalizeOptional(description),
            IsDefault = isDefault,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string name,
        Guid? locationId,
        string? description,
        bool isDefault,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        LocationId = NormalizeGuid(locationId);
        Description = NormalizeOptional(description);
        IsDefault = isDefault;
        UpdatedAtUtc = utcNow;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid? NormalizeGuid(Guid? value) =>
        value is null || value == Guid.Empty ? null : value;
}
