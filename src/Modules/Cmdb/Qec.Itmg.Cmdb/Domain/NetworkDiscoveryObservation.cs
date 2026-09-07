namespace Qec.Itmg.Cmdb.Domain;

public enum NetworkDiscoveryMatchStatus
{
    Matched = 0,
    PossibleMatch = 1,
    New = 2,
    Changed = 3,
    Ignored = 4,
}

public enum NetworkDiscoveryReviewStatus
{
    Pending = 0,
    Matched = 1,
    Created = 2,
    Updated = 3,
    Ignored = 4,
}

public sealed class NetworkDiscoveryObservation
{
    private NetworkDiscoveryObservation()
    {
    }

    public Guid Id { get; private set; }

    public Guid RunId { get; private set; }

    public string IpAddress { get; private set; } = null!;

    public string? Hostname { get; private set; }

    public string? MacAddress { get; private set; }

    public string? Vendor { get; private set; }

    public int? ResponseMs { get; private set; }

    public NetworkDiscoveryMatchStatus MatchStatus { get; private set; }

    public Guid? MatchedConfigurationItemId { get; private set; }

    public DateTimeOffset ObservedAtUtc { get; private set; }

    public NetworkDiscoveryReviewStatus ReviewStatus { get; private set; }

    public static NetworkDiscoveryObservation Create(
        Guid runId,
        string ipAddress,
        DateTimeOffset utcNow,
        string? hostname = null,
        string? macAddress = null,
        string? vendor = null,
        int? responseMs = null,
        NetworkDiscoveryMatchStatus matchStatus = NetworkDiscoveryMatchStatus.New,
        Guid? matchedConfigurationItemId = null)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run is required.", nameof(runId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);

        return new NetworkDiscoveryObservation
        {
            Id = Guid.CreateVersion7(),
            RunId = runId,
            IpAddress = ipAddress.Trim(),
            Hostname = NormalizeOptional(hostname),
            MacAddress = NormalizeOptional(macAddress),
            Vendor = NormalizeOptional(vendor),
            ResponseMs = responseMs,
            MatchStatus = matchStatus,
            MatchedConfigurationItemId = matchedConfigurationItemId is null || matchedConfigurationItemId == Guid.Empty
                ? null
                : matchedConfigurationItemId,
            ObservedAtUtc = utcNow,
            ReviewStatus = NetworkDiscoveryReviewStatus.Pending,
        };
    }

    public void MarkMatched(Guid configurationItemId)
    {
        if (configurationItemId == Guid.Empty)
        {
            throw new ArgumentException("Configuration item is required.", nameof(configurationItemId));
        }

        MatchedConfigurationItemId = configurationItemId;
        MatchStatus = NetworkDiscoveryMatchStatus.Matched;
        ReviewStatus = NetworkDiscoveryReviewStatus.Matched;
    }

    public void MarkCreated(Guid configurationItemId)
    {
        if (configurationItemId == Guid.Empty)
        {
            throw new ArgumentException("Configuration item is required.", nameof(configurationItemId));
        }

        MatchedConfigurationItemId = configurationItemId;
        MatchStatus = NetworkDiscoveryMatchStatus.Matched;
        ReviewStatus = NetworkDiscoveryReviewStatus.Created;
    }

    public void MarkUpdated(Guid configurationItemId)
    {
        if (configurationItemId == Guid.Empty)
        {
            throw new ArgumentException("Configuration item is required.", nameof(configurationItemId));
        }

        MatchedConfigurationItemId = configurationItemId;
        MatchStatus = NetworkDiscoveryMatchStatus.Matched;
        ReviewStatus = NetworkDiscoveryReviewStatus.Updated;
    }

    public void MarkIgnored()
    {
        MatchStatus = NetworkDiscoveryMatchStatus.Ignored;
        ReviewStatus = NetworkDiscoveryReviewStatus.Ignored;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
