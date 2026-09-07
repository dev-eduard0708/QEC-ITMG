namespace Qec.Itmg.Cmdb.Domain;

public sealed class CiNetworkIdentity
{
    private CiNetworkIdentity()
    {
    }

    public Guid Id { get; private set; }

    public Guid ConfigurationItemId { get; private set; }

    public string IpAddress { get; private set; } = null!;

    public string? Hostname { get; private set; }

    public string? MacAddress { get; private set; }

    public bool IsPrimary { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static CiNetworkIdentity Create(
        Guid configurationItemId,
        string ipAddress,
        DateTimeOffset utcNow,
        string? hostname = null,
        string? macAddress = null,
        bool isPrimary = false)
    {
        if (configurationItemId == Guid.Empty)
        {
            throw new ArgumentException("Configuration item is required.", nameof(configurationItemId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);

        return new CiNetworkIdentity
        {
            Id = Guid.CreateVersion7(),
            ConfigurationItemId = configurationItemId,
            IpAddress = ipAddress.Trim(),
            Hostname = NormalizeOptional(hostname),
            MacAddress = NormalizeOptional(macAddress),
            IsPrimary = isPrimary,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string ipAddress,
        string? hostname,
        string? macAddress,
        bool isPrimary,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);
        IpAddress = ipAddress.Trim();
        Hostname = NormalizeOptional(hostname);
        MacAddress = NormalizeOptional(macAddress);
        IsPrimary = isPrimary;
        UpdatedAtUtc = utcNow;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
