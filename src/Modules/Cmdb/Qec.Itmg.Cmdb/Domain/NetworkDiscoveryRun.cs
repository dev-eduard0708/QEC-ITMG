namespace Qec.Itmg.Cmdb.Domain;

public enum NetworkDiscoveryRunStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
}

public sealed class NetworkDiscoveryRun
{
    private NetworkDiscoveryRun()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProfileId { get; private set; }

    public NetworkDiscoveryRunStatus Status { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public Guid StartedByUserId { get; private set; }

    public int AddressesScanned { get; private set; }

    public int ResponsiveHosts { get; private set; }

    public string? ErrorSummary { get; private set; }

    public static NetworkDiscoveryRun Start(
        Guid profileId,
        Guid startedByUserId,
        DateTimeOffset utcNow)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile is required.", nameof(profileId));
        }

        if (startedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Started-by user is required.", nameof(startedByUserId));
        }

        return new NetworkDiscoveryRun
        {
            Id = Guid.CreateVersion7(),
            ProfileId = profileId,
            Status = NetworkDiscoveryRunStatus.Running,
            StartedAtUtc = utcNow,
            StartedByUserId = startedByUserId,
            AddressesScanned = 0,
            ResponsiveHosts = 0,
        };
    }

    public void UpdateProgress(int addressesScanned, int responsiveHosts)
    {
        AddressesScanned = Math.Max(0, addressesScanned);
        ResponsiveHosts = Math.Max(0, responsiveHosts);
    }

    public void Complete(int addressesScanned, int responsiveHosts, DateTimeOffset utcNow)
    {
        Status = NetworkDiscoveryRunStatus.Completed;
        AddressesScanned = Math.Max(0, addressesScanned);
        ResponsiveHosts = Math.Max(0, responsiveHosts);
        CompletedAtUtc = utcNow;
        ErrorSummary = null;
    }

    public void Fail(string errorSummary, DateTimeOffset utcNow)
    {
        Status = NetworkDiscoveryRunStatus.Failed;
        CompletedAtUtc = utcNow;
        ErrorSummary = string.IsNullOrWhiteSpace(errorSummary) ? "Discovery run failed." : errorSummary.Trim();
    }

    public void Cancel(DateTimeOffset utcNow)
    {
        Status = NetworkDiscoveryRunStatus.Cancelled;
        CompletedAtUtc = utcNow;
    }
}
