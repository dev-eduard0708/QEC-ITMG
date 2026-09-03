namespace Qec.Itmg.Cmdb.Domain;

public enum ConfigurationItemStatus
{
    Active = 0,
    Inactive = 1,
    Retired = 2,
}

public enum CiCriticality
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
}

public sealed class ConfigurationItem
{
    private ConfigurationItem()
    {
    }

    public Guid Id { get; private set; }

    public string CiNumber { get; private set; } = null!;

    public Guid CiTypeId { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public ConfigurationItemStatus Status { get; private set; }

    public CiCriticality? Criticality { get; private set; }

    public Guid? LocationId { get; private set; }

    public Guid? DepartmentId { get; private set; }

    public Guid? OwnerUserId { get; private set; }

    public string? SerialNumber { get; private set; }

    public string? Manufacturer { get; private set; }

    public string? Model { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static ConfigurationItem Create(
        string ciNumber,
        Guid ciTypeId,
        string name,
        DateTimeOffset utcNow,
        string? description = null,
        CiCriticality? criticality = null,
        Guid? locationId = null,
        Guid? departmentId = null,
        Guid? ownerUserId = null,
        string? serialNumber = null,
        string? manufacturer = null,
        string? model = null,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (ciTypeId == Guid.Empty)
        {
            throw new ArgumentException("CI type is required.", nameof(ciTypeId));
        }

        return new ConfigurationItem
        {
            Id = Guid.CreateVersion7(),
            CiNumber = ciNumber.Trim(),
            CiTypeId = ciTypeId,
            Name = name.Trim(),
            Description = NormalizeOptional(description),
            Status = ConfigurationItemStatus.Active,
            Criticality = criticality,
            LocationId = NormalizeGuid(locationId),
            DepartmentId = NormalizeGuid(departmentId),
            OwnerUserId = NormalizeGuid(ownerUserId),
            SerialNumber = NormalizeOptional(serialNumber),
            Manufacturer = NormalizeOptional(manufacturer),
            Model = NormalizeOptional(model),
            Notes = NormalizeOptional(notes),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void UpdateProfile(
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
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Name = name.Trim();
        Description = NormalizeOptional(description);
        Status = status;
        Criticality = criticality;
        LocationId = NormalizeGuid(locationId);
        DepartmentId = NormalizeGuid(departmentId);
        OwnerUserId = NormalizeGuid(ownerUserId);
        SerialNumber = NormalizeOptional(serialNumber);
        Manufacturer = NormalizeOptional(manufacturer);
        Model = NormalizeOptional(model);
        Notes = NormalizeOptional(notes);
        UpdatedAtUtc = utcNow;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid? NormalizeGuid(Guid? value) =>
        value is null || value == Guid.Empty ? null : value;
}
