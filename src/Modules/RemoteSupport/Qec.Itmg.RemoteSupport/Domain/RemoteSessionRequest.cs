namespace Qec.Itmg.RemoteSupport.Domain;

public enum RemoteSessionType
{
    Attended = 0,
    Unattended = 1,
}

public enum RemoteSessionStatus
{
    Requested = 0,
    NotifyUser = 1,
    Allowed = 2,
    Declined = 3,
    Expired = 4,
    Connecting = 5,
    InSession = 6,
    Ended = 7,
    Authorized = 8,
}

public enum RemoteSessionOutcome
{
    Completed = 0,
    Failed = 1,
    TerminatedByUser = 2,
    TerminatedByTechnician = 3,
    TerminatedBySystem = 4,
}

public sealed class RemoteSessionRequest
{
    private RemoteSessionRequest()
    {
    }

    public Guid Id { get; private set; }
    public string RemoteNumber { get; private set; } = null!;
    public Guid? ConfigurationItemId { get; private set; }
    public Guid? RemoteEndpointId { get; private set; }
    public Guid? TicketId { get; private set; }
    public Guid? ChangeRequestId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public Guid? TargetUserId { get; private set; }
    public Guid? TechnicianUserId { get; private set; }
    public string Reason { get; private set; } = null!;
    public string? RequestedPrivileges { get; private set; }
    public RemoteSessionType SessionType { get; private set; }
    public RemoteSessionStatus Status { get; private set; }
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public DateTimeOffset? AllowedAtUtc { get; private set; }
    public DateTimeOffset? DeclinedAtUtc { get; private set; }
    public DateTimeOffset? ConnectingAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? EndedAtUtc { get; private set; }
    public string? EngineSessionId { get; private set; }
    public string? EngineJoinUrl { get; private set; }
    public RemoteSessionOutcome? Outcome { get; private set; }
    public string? EndReason { get; private set; }
    public Guid? ConsentUserId { get; private set; }
    public string? ConsentIpAddress { get; private set; }
    public bool? ElevationUsed { get; private set; }
    public string? RecordingReference { get; private set; }
    public string? LastEngineError { get; private set; }
    public bool MfaSatisfiedAtStart { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static RemoteSessionRequest CreateAttended(
        string remoteNumber,
        Guid? configurationItemId,
        Guid requestedByUserId,
        Guid targetUserId,
        string reason,
        DateTimeOffset utcNow,
        TimeSpan consentTtl,
        Guid? ticketId = null,
        Guid? changeRequestId = null,
        string? requestedPrivileges = null,
        Guid? technicianUserId = null,
        Guid? remoteEndpointId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (targetUserId == Guid.Empty)
            throw new ArgumentException("Target user is required for attended support.", nameof(targetUserId));

        Guid? ci = NormalizeGuid(configurationItemId);
        Guid? endpoint = NormalizeGuid(remoteEndpointId);
        if (ci is null && endpoint is null)
            throw new ArgumentException("A managed configuration item or remote endpoint is required for IT-initiated attended support.");

        return new RemoteSessionRequest
        {
            Id = Guid.CreateVersion7(),
            RemoteNumber = remoteNumber.Trim(),
            ConfigurationItemId = ci,
            RemoteEndpointId = endpoint,
            TicketId = NormalizeGuid(ticketId),
            ChangeRequestId = NormalizeGuid(changeRequestId),
            RequestedByUserId = requestedByUserId,
            TargetUserId = targetUserId,
            TechnicianUserId = NormalizeGuid(technicianUserId) ?? requestedByUserId,
            Reason = reason.Trim(),
            RequestedPrivileges = NormalizeOptional(requestedPrivileges),
            SessionType = RemoteSessionType.Attended,
            Status = RemoteSessionStatus.NotifyUser,
            RequestedAtUtc = utcNow,
            ExpiresAtUtc = utcNow.Add(consentTtl),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    /// <summary>
    /// Employee self-service attended request. Technician may be unassigned; device may be prepared later.
    /// Consent window starts only when a technician requests remote access.
    /// </summary>
    public static RemoteSessionRequest CreateEmployeeSelfRequest(
        string remoteNumber,
        Guid employeeUserId,
        string reason,
        DateTimeOffset utcNow,
        Guid? ticketId = null,
        Guid? configurationItemId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (employeeUserId == Guid.Empty)
            throw new ArgumentException("Employee is required.", nameof(employeeUserId));

        return new RemoteSessionRequest
        {
            Id = Guid.CreateVersion7(),
            RemoteNumber = remoteNumber.Trim(),
            ConfigurationItemId = NormalizeGuid(configurationItemId),
            TicketId = NormalizeGuid(ticketId),
            RequestedByUserId = employeeUserId,
            TargetUserId = employeeUserId,
            TechnicianUserId = null,
            Reason = reason.Trim(),
            SessionType = RemoteSessionType.Attended,
            Status = RemoteSessionStatus.Requested,
            RequestedAtUtc = utcNow,
            ExpiresAtUtc = null,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public static RemoteSessionRequest CreateUnattended(
        string remoteNumber,
        Guid configurationItemId,
        Guid requestedByUserId,
        string reason,
        DateTimeOffset utcNow,
        Guid? ticketId = null,
        Guid? changeRequestId = null,
        string? requestedPrivileges = null,
        Guid? technicianUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (configurationItemId == Guid.Empty)
            throw new ArgumentException("Configuration item is required.", nameof(configurationItemId));

        return new RemoteSessionRequest
        {
            Id = Guid.CreateVersion7(),
            RemoteNumber = remoteNumber.Trim(),
            ConfigurationItemId = configurationItemId,
            RemoteEndpointId = null,
            TicketId = NormalizeGuid(ticketId),
            ChangeRequestId = NormalizeGuid(changeRequestId),
            RequestedByUserId = requestedByUserId,
            TechnicianUserId = NormalizeGuid(technicianUserId) ?? requestedByUserId,
            Reason = reason.Trim(),
            RequestedPrivileges = NormalizeOptional(requestedPrivileges),
            SessionType = RemoteSessionType.Unattended,
            Status = RemoteSessionStatus.Authorized,
            RequestedAtUtc = utcNow,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void MarkNotifyUser(DateTimeOffset utcNow)
    {
        EnsureTransition(RemoteSessionStatus.NotifyUser);
        Status = RemoteSessionStatus.NotifyUser;
        UpdatedAtUtc = utcNow;
    }

    public void AssignTechnician(Guid technicianUserId, DateTimeOffset utcNow)
    {
        if (technicianUserId == Guid.Empty)
            throw new ArgumentException("Technician is required.", nameof(technicianUserId));
        if (Status is RemoteSessionStatus.Ended or RemoteSessionStatus.Declined or RemoteSessionStatus.Expired)
            throw new InvalidOperationException("Cannot assign technician on a closed request.");

        TechnicianUserId = technicianUserId;
        UpdatedAtUtc = utcNow;
    }

    public void BindRemoteEndpoint(Guid remoteEndpointId, DateTimeOffset utcNow)
    {
        if (remoteEndpointId == Guid.Empty)
            throw new ArgumentException("Endpoint is required.", nameof(remoteEndpointId));
        RemoteEndpointId = remoteEndpointId;
        UpdatedAtUtc = utcNow;
    }

    public void BindConfigurationItem(Guid configurationItemId, DateTimeOffset utcNow)
    {
        if (configurationItemId == Guid.Empty)
            throw new ArgumentException("Configuration item is required.", nameof(configurationItemId));
        ConfigurationItemId = configurationItemId;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Technician asks the employee for remote-control consent (distinct from chat).</summary>
    public void RequestEmployeeAccess(DateTimeOffset utcNow, TimeSpan consentTtl)
    {
        if (SessionType != RemoteSessionType.Attended)
            throw new InvalidOperationException("Only attended sessions use employee consent.");
        if (Status is RemoteSessionStatus.Ended or RemoteSessionStatus.Declined or RemoteSessionStatus.Expired)
            throw new InvalidOperationException("Request is closed.");
        if (Status is RemoteSessionStatus.Connecting or RemoteSessionStatus.InSession)
            throw new InvalidOperationException("Session is already connecting or active.");
        if (Status == RemoteSessionStatus.Allowed)
            throw new InvalidOperationException("Employee already allowed remote access.");

        if (consentTtl < TimeSpan.FromMinutes(1))
            consentTtl = TimeSpan.FromMinutes(1);

        Status = RemoteSessionStatus.NotifyUser;
        ExpiresAtUtc = utcNow.Add(consentTtl);
        AllowedAtUtc = null;
        DeclinedAtUtc = null;
        ConsentUserId = null;
        ConsentIpAddress = null;
        UpdatedAtUtc = utcNow;
    }

    public bool HasConnectTarget =>
        ConfigurationItemId is not null || RemoteEndpointId is not null;

    public void Allow(Guid consentUserId, string? ipAddress, DateTimeOffset utcNow)
    {
        if (SessionType != RemoteSessionType.Attended)
            throw new InvalidOperationException("Only attended requests accept employee consent.");
        if (Status is not (RemoteSessionStatus.NotifyUser or RemoteSessionStatus.Requested))
            throw new InvalidOperationException($"Cannot allow from status {Status}.");
        if (ExpiresAtUtc is DateTimeOffset exp && utcNow > exp)
        {
            Expire(utcNow);
            throw new InvalidOperationException("Remote support request has expired.");
        }

        if (TargetUserId is Guid target && consentUserId != target)
            throw new InvalidOperationException("Only the target user may consent to this remote session.");

        Status = RemoteSessionStatus.Allowed;
        AllowedAtUtc = utcNow;
        ConsentUserId = consentUserId;
        ConsentIpAddress = NormalizeOptional(ipAddress);
        UpdatedAtUtc = utcNow;
    }

    public void Decline(Guid consentUserId, string? ipAddress, DateTimeOffset utcNow)
    {
        if (SessionType != RemoteSessionType.Attended)
            throw new InvalidOperationException("Only attended requests accept employee decline.");
        if (Status is not (RemoteSessionStatus.NotifyUser or RemoteSessionStatus.Requested))
            throw new InvalidOperationException($"Cannot decline from status {Status}.");

        if (TargetUserId is Guid target && consentUserId != target)
            throw new InvalidOperationException("Only the target user may decline this remote session.");

        Status = RemoteSessionStatus.Declined;
        DeclinedAtUtc = utcNow;
        ConsentUserId = consentUserId;
        ConsentIpAddress = NormalizeOptional(ipAddress);
        Outcome = RemoteSessionOutcome.TerminatedByUser;
        EndReason = "Declined by user";
        EndedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public void Expire(DateTimeOffset utcNow)
    {
        if (Status is RemoteSessionStatus.Declined or RemoteSessionStatus.Expired or RemoteSessionStatus.Ended)
            return;
        if (Status is RemoteSessionStatus.Connecting or RemoteSessionStatus.InSession)
            throw new InvalidOperationException("Active sessions cannot be expired; end them instead.");

        Status = RemoteSessionStatus.Expired;
        Outcome = RemoteSessionOutcome.TerminatedBySystem;
        EndReason = "Consent expired";
        EndedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public void BeginConnecting(DateTimeOffset utcNow)
    {
        if (SessionType == RemoteSessionType.Attended && Status != RemoteSessionStatus.Allowed)
            throw new InvalidOperationException("Attended session requires Allowed consent before connect.");
        if (SessionType == RemoteSessionType.Unattended && Status != RemoteSessionStatus.Authorized)
            throw new InvalidOperationException("Unattended session is not authorized.");
        if (ExpiresAtUtc is DateTimeOffset exp && utcNow > exp && SessionType == RemoteSessionType.Attended)
        {
            Expire(utcNow);
            throw new InvalidOperationException("Remote support request has expired.");
        }

        Status = RemoteSessionStatus.Connecting;
        ConnectingAtUtc = utcNow;
        LastEngineError = null;
        UpdatedAtUtc = utcNow;
    }

    public void MarkInSession(string engineSessionId, string? joinUrl, bool mfaSatisfied, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineSessionId);
        if (Status != RemoteSessionStatus.Connecting)
            throw new InvalidOperationException("Only Connecting requests can enter InSession.");

        Status = RemoteSessionStatus.InSession;
        EngineSessionId = engineSessionId.Trim();
        EngineJoinUrl = NormalizeOptional(joinUrl);
        StartedAtUtc = utcNow;
        MfaSatisfiedAtStart = mfaSatisfied;
        UpdatedAtUtc = utcNow;
    }

    public void MarkConnectFailed(string error, DateTimeOffset utcNow)
    {
        if (Status != RemoteSessionStatus.Connecting)
            return;

        LastEngineError = Truncate(error, 1000);
        // Roll back to Allowed/Authorized so technician can retry without faking InSession.
        Status = SessionType == RemoteSessionType.Attended
            ? RemoteSessionStatus.Allowed
            : RemoteSessionStatus.Authorized;
        ConnectingAtUtc = null;
        UpdatedAtUtc = utcNow;
    }

    public bool TryCompleteFromEngine(
        DateTimeOffset endedAtUtc,
        RemoteSessionOutcome outcome,
        string? endReason,
        bool? elevationUsed,
        string? recordingReference)
    {
        if (Status == RemoteSessionStatus.Ended && EndedAtUtc is not null)
            return false; // idempotent

        if (Status is not (RemoteSessionStatus.InSession or RemoteSessionStatus.Connecting))
            return false;

        Status = RemoteSessionStatus.Ended;
        EndedAtUtc = endedAtUtc;
        Outcome = outcome;
        EndReason = NormalizeOptional(endReason);
        ElevationUsed = elevationUsed;
        RecordingReference = NormalizeOptional(recordingReference);
        UpdatedAtUtc = endedAtUtc;
        return true;
    }

    public void EndByActor(
        RemoteSessionOutcome outcome,
        string? endReason,
        DateTimeOffset utcNow)
    {
        if (Status is RemoteSessionStatus.Ended or RemoteSessionStatus.Declined or RemoteSessionStatus.Expired)
            return;

        Status = RemoteSessionStatus.Ended;
        EndedAtUtc = utcNow;
        Outcome = outcome;
        EndReason = NormalizeOptional(endReason);
        UpdatedAtUtc = utcNow;
    }

    private void EnsureTransition(RemoteSessionStatus next)
    {
        _ = next;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid? NormalizeGuid(Guid? value) =>
        value is null || value == Guid.Empty ? null : value;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
