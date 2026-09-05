namespace Qec.Itmg.RemoteSupport.Domain;

public enum RemoteEndpointKind
{
    Managed = 0,
    Temporary = 1,
}

public enum RemoteEndpointConnectionStatus
{
    Registering = 0,
    Online = 1,
    Offline = 2,
    Expired = 3,
}

public sealed class RemoteEndpoint
{
    private RemoteEndpoint()
    {
    }

    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid? CurrentRemoteSessionRequestId { get; private set; }
    public Guid? ConfigurationItemId { get; private set; }
    public string? EngineNodeId { get; private set; }
    public RemoteEndpointKind EndpointKind { get; private set; }
    public string DeviceName { get; private set; } = null!;
    public string OperatingSystem { get; private set; } = null!;
    public string? OperatingSystemVersion { get; private set; }
    public string? Architecture { get; private set; }
    public string? HelperVersion { get; private set; }
    public string? AgentVersion { get; private set; }
    public RemoteEndpointConnectionStatus ConnectionStatus { get; private set; }
    public DateTimeOffset FirstSeenAtUtc { get; private set; }
    public DateTimeOffset LastSeenAtUtc { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public bool IsReadyForRemote =>
        ConnectionStatus == RemoteEndpointConnectionStatus.Online
        && !string.IsNullOrWhiteSpace(EngineNodeId);

    /// <summary>Temporary endpoints are never ready for unattended; attended needs engine node.</summary>
    public bool HasEngineNode => !string.IsNullOrWhiteSpace(EngineNodeId);

    public static RemoteEndpoint CreateTemporary(
        Guid ownerUserId,
        Guid sessionRequestId,
        string deviceName,
        string operatingSystem,
        DateTimeOffset utcNow,
        TimeSpan? retention,
        string? operatingSystemVersion = null,
        string? architecture = null,
        string? helperVersion = null,
        string? engineNodeId = null)
    {
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("Owner is required.", nameof(ownerUserId));
        if (sessionRequestId == Guid.Empty)
            throw new ArgumentException("Session is required.", nameof(sessionRequestId));
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(operatingSystem);

        DateTimeOffset now = utcNow;
        return new RemoteEndpoint
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = ownerUserId,
            CurrentRemoteSessionRequestId = sessionRequestId,
            EndpointKind = RemoteEndpointKind.Temporary,
            DeviceName = Truncate(deviceName.Trim(), 128),
            OperatingSystem = Truncate(operatingSystem.Trim(), 64),
            OperatingSystemVersion = NormalizeOptional(operatingSystemVersion, 64),
            Architecture = NormalizeOptional(architecture, 32),
            HelperVersion = NormalizeOptional(helperVersion, 32),
            EngineNodeId = NormalizeOptional(engineNodeId, 128),
            ConnectionStatus = string.IsNullOrWhiteSpace(engineNodeId)
                ? RemoteEndpointConnectionStatus.Registering
                : RemoteEndpointConnectionStatus.Online,
            FirstSeenAtUtc = now,
            LastSeenAtUtc = now,
            ExpiresAtUtc = retention is TimeSpan r ? now.Add(r) : now.AddHours(72),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public static RemoteEndpoint CreateManagedProjection(
        Guid ownerUserId,
        Guid configurationItemId,
        string deviceName,
        string? engineNodeId,
        DateTimeOffset utcNow,
        Guid? sessionRequestId = null)
    {
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("Owner is required.", nameof(ownerUserId));
        if (configurationItemId == Guid.Empty)
            throw new ArgumentException("Configuration item is required.", nameof(configurationItemId));
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);

        bool online = !string.IsNullOrWhiteSpace(engineNodeId);
        return new RemoteEndpoint
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = ownerUserId,
            CurrentRemoteSessionRequestId = sessionRequestId,
            ConfigurationItemId = configurationItemId,
            EngineNodeId = NormalizeOptional(engineNodeId, 128),
            EndpointKind = RemoteEndpointKind.Managed,
            DeviceName = Truncate(deviceName.Trim(), 128),
            OperatingSystem = "Managed",
            ConnectionStatus = online
                ? RemoteEndpointConnectionStatus.Online
                : RemoteEndpointConnectionStatus.Registering,
            FirstSeenAtUtc = utcNow,
            LastSeenAtUtc = utcNow,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void TouchHeartbeat(DateTimeOffset utcNow, string? connectionStatus = null)
    {
        LastSeenAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
        if (!string.IsNullOrWhiteSpace(connectionStatus)
            && Enum.TryParse(connectionStatus, true, out RemoteEndpointConnectionStatus st)
            && st != RemoteEndpointConnectionStatus.Expired)
        {
            ConnectionStatus = st;
        }
        else if (ConnectionStatus == RemoteEndpointConnectionStatus.Registering && HasEngineNode)
        {
            ConnectionStatus = RemoteEndpointConnectionStatus.Online;
        }
    }

    public void SetEngineNode(string? engineNodeId, DateTimeOffset utcNow)
    {
        EngineNodeId = NormalizeOptional(engineNodeId, 128);
        LastSeenAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
        if (HasEngineNode && ConnectionStatus == RemoteEndpointConnectionStatus.Registering)
            ConnectionStatus = RemoteEndpointConnectionStatus.Online;
    }

    public void BindSession(Guid sessionRequestId, DateTimeOffset utcNow)
    {
        CurrentRemoteSessionRequestId = sessionRequestId;
        UpdatedAtUtc = utcNow;
    }

    public void LinkToConfigurationItem(Guid configurationItemId, DateTimeOffset utcNow)
    {
        if (configurationItemId == Guid.Empty)
            throw new ArgumentException("Configuration item is required.", nameof(configurationItemId));
        ConfigurationItemId = configurationItemId;
        // Kind stays Temporary until admin explicitly promotes — linking does not auto-promote.
        UpdatedAtUtc = utcNow;
    }

    public void MarkExpired(DateTimeOffset utcNow)
    {
        ConnectionStatus = RemoteEndpointConnectionStatus.Expired;
        EngineNodeId = null;
        CurrentRemoteSessionRequestId = null;
        ExpiresAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    private static string? NormalizeOptional(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), max);

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
