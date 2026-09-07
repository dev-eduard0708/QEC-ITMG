namespace Qec.Itmg.Cmdb.Domain;

public sealed class NetworkDiscoveryProfile
{
    private NetworkDiscoveryProfile()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string Cidr { get; private set; } = null!;

    public Guid? LocationId { get; private set; }

    public bool IsActive { get; private set; }

    public int TimeoutMs { get; private set; }

    public int MaxConcurrency { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static NetworkDiscoveryProfile Create(
        string name,
        string cidr,
        DateTimeOffset utcNow,
        Guid? locationId = null,
        int timeoutMs = 1000,
        int maxConcurrency = 32,
        bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(cidr);
        if (timeoutMs < 100 || timeoutMs > 30_000)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "Timeout must be between 100 and 30000 ms.");
        }

        if (maxConcurrency < 1 || maxConcurrency > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Max concurrency must be between 1 and 32.");
        }

        return new NetworkDiscoveryProfile
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Cidr = cidr.Trim(),
            LocationId = locationId is null || locationId == Guid.Empty ? null : locationId,
            IsActive = isActive,
            TimeoutMs = timeoutMs,
            MaxConcurrency = maxConcurrency,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string name,
        string cidr,
        Guid? locationId,
        bool isActive,
        int timeoutMs,
        int maxConcurrency,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(cidr);
        if (timeoutMs < 100 || timeoutMs > 30_000)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "Timeout must be between 100 and 30000 ms.");
        }

        if (maxConcurrency < 1 || maxConcurrency > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Max concurrency must be between 1 and 32.");
        }

        Name = name.Trim();
        Cidr = cidr.Trim();
        LocationId = locationId is null || locationId == Guid.Empty ? null : locationId;
        IsActive = isActive;
        TimeoutMs = timeoutMs;
        MaxConcurrency = maxConcurrency;
        UpdatedAtUtc = utcNow;
    }
}
