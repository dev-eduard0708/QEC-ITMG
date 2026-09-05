namespace Qec.Itmg.RemoteSupport.Domain;

public enum RemoteEndpointKind
{
    Managed = 0,
    Temporary = 1,
}

public enum RemoteEndpointConnectionStatus
{
    Registering = 0,
    WaitingForAgent = 1,
    AgentInstalling = 2,
    AgentOnline = 3,
    Ready = 4,
    Offline = 5,
    Failed = 6,
    Expired = 7,
    /// <summary>Legacy synonym retained for existing rows; treat like Ready when engine node present.</summary>
    Online = 8,
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
        !string.IsNullOrWhiteSpace(EngineNodeId)
        && ConnectionStatus is RemoteEndpointConnectionStatus.Ready
            or RemoteEndpointConnectionStatus.Online
            or RemoteEndpointConnectionStatus.AgentOnline;

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
        if (sessionRequestId == Guid.Empty)
            throw new ArgumentException("Session is required.", nameof(sessionRequestId));

        RemoteEndpoint endpoint = CreatePairedTemporary(
            ownerUserId,
            deviceName,
            operatingSystem,
            utcNow,
            retention,
            operatingSystemVersion,
            architecture,
            helperVersion,
            engineNodeId);
        endpoint.CurrentRemoteSessionRequestId = sessionRequestId;
        return endpoint;
    }

    /// <summary>
    /// Pre-session pairing: temporary personal endpoint owned by the authenticated employee.
    /// </summary>
    public static RemoteEndpoint CreatePairedTemporary(
        Guid ownerUserId,
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
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(operatingSystem);

        DateTimeOffset now = utcNow;
        return new RemoteEndpoint
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = ownerUserId,
            EndpointKind = RemoteEndpointKind.Temporary,
            DeviceName = Truncate(deviceName.Trim(), 128),
            OperatingSystem = Truncate(operatingSystem.Trim(), 64),
            OperatingSystemVersion = NormalizeOptional(operatingSystemVersion, 64),
            Architecture = NormalizeOptional(architecture, 32),
            HelperVersion = NormalizeOptional(helperVersion, 32),
            EngineNodeId = NormalizeOptional(engineNodeId, 256),
            ConnectionStatus = string.IsNullOrWhiteSpace(engineNodeId)
                ? RemoteEndpointConnectionStatus.WaitingForAgent
                : RemoteEndpointConnectionStatus.Ready,
            FirstSeenAtUtc = now,
            LastSeenAtUtc = now,
            ExpiresAtUtc = retention is TimeSpan r ? now.Add(r) : now.AddHours(72),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    /// <summary>Employee removes Temporary pairing. Does not touch Asset/CI records.</summary>
    public void Unpair(DateTimeOffset utcNow)
    {
        if (EndpointKind != RemoteEndpointKind.Temporary)
            throw new InvalidOperationException("Only temporary personal computers can be removed here.");
        MarkExpired(utcNow);
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
            EngineNodeId = NormalizeOptional(engineNodeId, 256),
            EndpointKind = RemoteEndpointKind.Managed,
            DeviceName = Truncate(deviceName.Trim(), 128),
            OperatingSystem = "Managed",
            ConnectionStatus = online
                ? RemoteEndpointConnectionStatus.Ready
                : RemoteEndpointConnectionStatus.WaitingForAgent,
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
    }

    public void MarkWaitingForAgent(DateTimeOffset utcNow)
    {
        ConnectionStatus = RemoteEndpointConnectionStatus.WaitingForAgent;
        UpdatedAtUtc = utcNow;
        LastSeenAtUtc = utcNow;
    }

    public void MarkAgentInstalling(DateTimeOffset utcNow)
    {
        ConnectionStatus = RemoteEndpointConnectionStatus.AgentInstalling;
        UpdatedAtUtc = utcNow;
        LastSeenAtUtc = utcNow;
    }

    public void MarkAgentOnline(DateTimeOffset utcNow)
    {
        ConnectionStatus = RemoteEndpointConnectionStatus.AgentOnline;
        UpdatedAtUtc = utcNow;
        LastSeenAtUtc = utcNow;
    }

    public void MarkReady(string engineNodeId, DateTimeOffset utcNow)
    {
        EngineNodeId = NormalizeOptional(engineNodeId, 256)
            ?? throw new ArgumentException("Engine node is required for Ready.", nameof(engineNodeId));
        ConnectionStatus = RemoteEndpointConnectionStatus.Ready;
        UpdatedAtUtc = utcNow;
        LastSeenAtUtc = utcNow;
    }

    public void MarkOffline(DateTimeOffset utcNow)
    {
        ConnectionStatus = RemoteEndpointConnectionStatus.Offline;
        UpdatedAtUtc = utcNow;
        LastSeenAtUtc = utcNow;
    }

    public void MarkFailed(DateTimeOffset utcNow)
    {
        ConnectionStatus = RemoteEndpointConnectionStatus.Failed;
        UpdatedAtUtc = utcNow;
        LastSeenAtUtc = utcNow;
    }

    public void SetEngineNode(string? engineNodeId, DateTimeOffset utcNow)
    {
        EngineNodeId = NormalizeOptional(engineNodeId, 256);
        LastSeenAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
        if (HasEngineNode
            && ConnectionStatus is RemoteEndpointConnectionStatus.Registering
                or RemoteEndpointConnectionStatus.WaitingForAgent
                or RemoteEndpointConnectionStatus.AgentInstalling
                or RemoteEndpointConnectionStatus.AgentOnline)
        {
            ConnectionStatus = RemoteEndpointConnectionStatus.Ready;
        }
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
