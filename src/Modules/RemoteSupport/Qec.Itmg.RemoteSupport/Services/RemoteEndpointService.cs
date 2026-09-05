using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.RemoteSupport;
using Qec.Itmg.RemoteSupport.Domain;
using Qec.Itmg.RemoteSupport.Persistence;

namespace Qec.Itmg.RemoteSupport.Services;

public sealed record RemoteEndpointDto(
    Guid Id,
    Guid OwnerUserId,
    Guid? CurrentRemoteSessionRequestId,
    Guid? ConfigurationItemId,
    string? EngineNodeId,
    string EndpointKind,
    string DeviceName,
    string OperatingSystem,
    string? OperatingSystemVersion,
    string? Architecture,
    string? HelperVersion,
    string? AgentVersion,
    string ConnectionStatus,
    bool IsReadyForRemote,
    bool HasEngineNode,
    DateTimeOffset FirstSeenAtUtc,
    DateTimeOffset LastSeenAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string RowVersion);

public sealed record EnrollmentIssueResult(
    Guid EnrollmentId,
    string Token,
    DateTimeOffset ExpiresAtUtc,
    bool HelperDownloadConfigured,
    string? HelperDownloadUrl,
    string? HelperInstallInstructions,
    bool AgentDownloadConfigured,
    string? AgentDownloadUrl,
    string? AgentInstallInstructions,
    bool SessionBoundHelperPackageAvailable);

public sealed record EnrollmentRedeemRequest(
    string Token,
    string DeviceName,
    string OperatingSystem,
    string? OperatingSystemVersion,
    string? Architecture,
    string? HelperVersion,
    string? ReportedEngineNodeId,
    string? AgentStatus);

public sealed record EnrollmentRedeemResult(
    Guid EndpointId,
    Guid RemoteSessionRequestId,
    string DeviceName,
    string ConnectionStatus,
    bool WaitingForRemoteAgent,
    string? AgentDownloadUrl,
    string? AgentInstallInstructions,
    string? AgentBootstrapUrl,
    string ReportSecret);

public sealed record AgentBootstrapInfo(
    bool Available,
    string? AgentDownloadUrl,
    string? AgentInstallInstructions,
    string? InviteUrl,
    string MeshDeviceGroupId);

/// <summary>
/// Real MeshCentral enrollment: agent URLs + node presence resolution via control.ashx.
/// </summary>
public interface IRemoteEndpointEnrollmentEngine
{
    AgentBootstrapInfo GetAgentBootstrap();

    Task<string?> TryResolveOrProvisionNodeAsync(
        Guid endpointId,
        string deviceName,
        string? reportedEngineNodeId,
        CancellationToken cancellationToken = default);

    Task SynchronizePresenceAsync(
        RemoteEndpoint endpoint,
        CancellationToken cancellationToken = default);
}

public sealed class DeferredRemoteEndpointEnrollmentEngine(IOptions<RemoteSupportOptions> options)
    : IRemoteEndpointEnrollmentEngine
{
    public AgentBootstrapInfo GetAgentBootstrap()
    {
        RemoteSupportOptions cfg = options.Value;
        string? url = cfg.HasAgentDownload ? cfg.AgentDownloadUrl.Trim() : null;
        return new AgentBootstrapInfo(
            url is not null,
            url,
            string.IsNullOrWhiteSpace(cfg.AgentInstallInstructions) ? null : cfg.AgentInstallInstructions.Trim(),
            null,
            cfg.MeshDeviceGroupId);
    }

    public Task<string?> TryResolveOrProvisionNodeAsync(
        Guid endpointId,
        string deviceName,
        string? reportedEngineNodeId,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(reportedEngineNodeId))
            return Task.FromResult<string?>(reportedEngineNodeId.Trim());
        return Task.FromResult<string?>(null);
    }

    public Task SynchronizePresenceAsync(RemoteEndpoint endpoint, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class MeshCentralEndpointEnrollmentEngine(
    IOptions<RemoteSupportOptions> options,
    MeshCentralRemoteSupportEngine meshEngine,
    ILogger<MeshCentralEndpointEnrollmentEngine> logger) : IRemoteEndpointEnrollmentEngine
{
    public AgentBootstrapInfo GetAgentBootstrap()
    {
        RemoteSupportOptions cfg = options.Value;
        if (!cfg.IsConfigured || !cfg.HasMeshDeviceGroup)
        {
            string? fallback = cfg.HasAgentDownload ? cfg.AgentDownloadUrl.Trim() : null;
            return new AgentBootstrapInfo(
                fallback is not null,
                fallback,
                string.IsNullOrWhiteSpace(cfg.AgentInstallInstructions) ? null : cfg.AgentInstallInstructions.Trim(),
                null,
                cfg.MeshDeviceGroupId);
        }

        // Native MeshCentral agent download (MeshCtrl AgentDownload equivalent).
        string agentUrl =
            $"{cfg.BaseUrl.TrimEnd('/')}/meshagents?id={cfg.WindowsAgentTypeId}&meshid={Uri.EscapeDataString(cfg.MeshDeviceGroupId.Trim())}";
        string instructions =
            "Run the MeshCentral agent installer. When installation finishes, return to Remote Support — the device status updates automatically.";
        if (!string.IsNullOrWhiteSpace(cfg.AgentInstallInstructions))
            instructions = cfg.AgentInstallInstructions.Trim();

        return new AgentBootstrapInfo(true, agentUrl, instructions, null, cfg.MeshDeviceGroupId.Trim());
    }

    public async Task<string?> TryResolveOrProvisionNodeAsync(
        Guid endpointId,
        string deviceName,
        string? reportedEngineNodeId,
        CancellationToken cancellationToken = default)
    {
        RemoteSupportOptions cfg = options.Value;
        if (!cfg.IsConfigured)
            return null;

        try
        {
            IReadOnlyList<MeshCentral.MeshCentralNode> nodes = await meshEngine.ListNodesAsync(cancellationToken);
            MeshCentral.MeshCentralNode? match = null;
            if (!string.IsNullOrWhiteSpace(reportedEngineNodeId))
            {
                string reported = reportedEngineNodeId.Trim();
                match = nodes.FirstOrDefault(n =>
                    string.Equals(n.NodeId, reported, StringComparison.OrdinalIgnoreCase)
                    || n.NodeId.EndsWith(reported, StringComparison.OrdinalIgnoreCase)
                    || reported.EndsWith(n.NodeId, StringComparison.OrdinalIgnoreCase));
            }

            match ??= nodes.FirstOrDefault(n =>
                string.Equals(n.Name, deviceName, StringComparison.OrdinalIgnoreCase));

            // Ready requires a real, currently online MeshCentral node.
            return match is { Online: true } ? match.NodeId : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MeshCentral node resolve failed for endpoint {EndpointId}", endpointId);
            return null;
        }
    }

    public async Task SynchronizePresenceAsync(RemoteEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        RemoteSupportOptions cfg = options.Value;
        if (!cfg.IsConfigured || string.IsNullOrWhiteSpace(endpoint.EngineNodeId))
            return;

        try
        {
            IReadOnlyList<MeshCentral.MeshCentralNode> nodes = await meshEngine.ListNodesAsync(cancellationToken);
            MeshCentral.MeshCentralNode? match = nodes.FirstOrDefault(n =>
                string.Equals(n.NodeId, endpoint.EngineNodeId, StringComparison.OrdinalIgnoreCase)
                || n.NodeId.EndsWith(endpoint.EngineNodeId!, StringComparison.OrdinalIgnoreCase));
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (match is null)
            {
                endpoint.MarkOffline(now);
                return;
            }

            if (match.Online)
                endpoint.MarkReady(match.NodeId, now);
            else
                endpoint.MarkOffline(now);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Presence sync failed for endpoint {EndpointId}", endpoint.Id);
        }
    }
}


public sealed class RemoteEndpointService(
    RemoteSupportDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    IOptions<RemoteSupportOptions> options,
    IRemoteEndpointEnrollmentEngine enrollmentEngine,
    ILogger<RemoteEndpointService> logger)
{
    private static readonly ConcurrentDictionary<string, (int Count, DateTimeOffset WindowStart)> RedeemAttempts = new();
    private static readonly ConcurrentDictionary<Guid, string> ReportSecrets = new();

    public bool ValidateReportSecret(Guid endpointId, string? secret) =>
        !string.IsNullOrWhiteSpace(secret)
        && ReportSecrets.TryGetValue(endpointId, out string? expected)
        && CryptographicEquals(expected, secret);

    public async Task<RemoteEndpointDto?> GetAsync(Guid id, CancellationToken ct)
    {
        RemoteEndpoint? entity = await db.RemoteEndpoints.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return entity is null ? null : Map(entity);
    }

    public async Task<RemoteEndpointDto?> GetForSessionAsync(Guid sessionId, CancellationToken ct)
    {
        RemoteEndpoint? entity = await db.RemoteEndpoints.AsNoTracking()
            .Where(x => x.CurrentRemoteSessionRequestId == sessionId
                && x.ConnectionStatus != RemoteEndpointConnectionStatus.Expired)
            .OrderByDescending(x => x.LastSeenAtUtc)
            .FirstOrDefaultAsync(ct);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<RemoteEndpointDto>> ListAsync(
        string? kind,
        string? connectionStatus,
        bool includeExpired,
        int take,
        CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 200);
        IQueryable<RemoteEndpoint> q = db.RemoteEndpoints.AsNoTracking();
        if (!includeExpired)
            q = q.Where(x => x.ConnectionStatus != RemoteEndpointConnectionStatus.Expired);
        if (!string.IsNullOrWhiteSpace(kind)
            && Enum.TryParse(kind, true, out RemoteEndpointKind k))
            q = q.Where(x => x.EndpointKind == k);
        if (!string.IsNullOrWhiteSpace(connectionStatus)
            && Enum.TryParse(connectionStatus, true, out RemoteEndpointConnectionStatus st))
            q = q.Where(x => x.ConnectionStatus == st);

        List<RemoteEndpoint> items = await q
            .OrderByDescending(x => x.LastSeenAtUtc)
            .Take(take)
            .ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<EnrollmentIssueResult> IssueEnrollmentAsync(
        Guid sessionId,
        Guid userId,
        string? ipAddress,
        CancellationToken ct)
    {
        RemoteSessionRequest session = await db.RemoteSessionRequests
            .FirstOrDefaultAsync(x => x.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Remote session not found.");

        if (session.TargetUserId != userId)
            throw new InvalidOperationException("Only the employee on this request may prepare a computer.");
        if (session.Status is RemoteSessionStatus.Ended or RemoteSessionStatus.Declined or RemoteSessionStatus.Expired)
            throw new InvalidOperationException("This support request is closed.");

        // Revoke outstanding enrollments for this session/user.
        List<RemoteEndpointEnrollment> open = await db.RemoteEndpointEnrollments
            .Where(x => x.RemoteSessionRequestId == sessionId
                && x.UserId == userId
                && x.RedeemedAtUtc == null
                && x.RevokedAtUtc == null)
            .ToListAsync(ct);
        DateTimeOffset now = clock.UtcNow;
        foreach (RemoteEndpointEnrollment e in open)
            e.Revoke(now);

        TimeSpan lifetime = TimeSpan.FromMinutes(Math.Clamp(options.Value.EnrollmentTokenLifetimeMinutes, 2, 60));
        (RemoteEndpointEnrollment enrollment, string plain) = RemoteEndpointEnrollment.Issue(
            sessionId, userId, now, lifetime, ipAddress);
        db.RemoteEndpointEnrollments.Add(enrollment);
        await db.SaveChangesAsync(ct);

        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.RemoteSession,
            AggregateId = sessionId,
            BusinessNumber = session.RemoteNumber,
            Action = BusinessAuditAction.Created,
            FieldName = "RemoteEnrollmentIssued",
            NewValue = enrollment.Id.ToString("N"),
            Source = AuditSource.Api,
        }, ct);

        RemoteSupportOptions cfg = options.Value;
        AgentBootstrapInfo bootstrap = enrollmentEngine.GetAgentBootstrap();
        bool helperConfigured = cfg.HasHelperDownload || cfg.HasHelperArtifact;
        return new EnrollmentIssueResult(
            enrollment.Id,
            plain,
            enrollment.ExpiresAtUtc,
            helperConfigured,
            cfg.HasHelperDownload ? cfg.HelperDownloadUrl.Trim() : null,
            string.IsNullOrWhiteSpace(cfg.HelperInstallInstructions) ? null : cfg.HelperInstallInstructions.Trim(),
            bootstrap.Available,
            bootstrap.AgentDownloadUrl,
            bootstrap.AgentInstallInstructions,
            cfg.HasHelperArtifact);
    }

    public async Task<EnrollmentRedeemResult> RedeemEnrollmentAsync(
        EnrollmentRedeemRequest request,
        string? clientIp,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Token);
        EnforceRedeemRateLimit(clientIp ?? "unknown");

        string hash = RemoteEndpointEnrollment.HashToken(request.Token);
        RemoteEndpointEnrollment? enrollment = await db.RemoteEndpointEnrollments
            .FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (enrollment is null)
            throw new InvalidOperationException("Enrollment token is invalid.");

        DateTimeOffset now = clock.UtcNow;
        if (!enrollment.IsRedeemable(now))
            throw new InvalidOperationException(
                enrollment.RedeemedAtUtc is not null
                    ? "Enrollment token was already used."
                    : enrollment.RevokedAtUtc is not null
                        ? "Enrollment token was revoked."
                        : "Enrollment token has expired.");

        RemoteSessionRequest session = await db.RemoteSessionRequests
            .FirstOrDefaultAsync(x => x.Id == enrollment.RemoteSessionRequestId, ct)
            ?? throw new InvalidOperationException("Remote session not found.");

        if (session.Status is RemoteSessionStatus.Ended or RemoteSessionStatus.Declined or RemoteSessionStatus.Expired)
            throw new InvalidOperationException("This support request is closed.");

        string? nodeId = await enrollmentEngine.TryResolveOrProvisionNodeAsync(
            Guid.Empty,
            request.DeviceName,
            request.ReportedEngineNodeId,
            ct);

        TimeSpan retention = TimeSpan.FromHours(Math.Clamp(options.Value.TemporaryEndpointRetentionHours, 1, 24 * 30));
        RemoteEndpoint endpoint = RemoteEndpoint.CreateTemporary(
            enrollment.UserId,
            session.Id,
            request.DeviceName,
            request.OperatingSystem,
            now,
            retention,
            request.OperatingSystemVersion,
            request.Architecture,
            request.HelperVersion,
            nodeId);

        if (string.Equals(request.AgentStatus, "installing", StringComparison.OrdinalIgnoreCase))
            endpoint.MarkAgentInstalling(now);
        else if (nodeId is null)
            endpoint.MarkWaitingForAgent(now);

        enrollment.Redeem(endpoint.Id, now);
        session.BindRemoteEndpoint(endpoint.Id, now);
        db.RemoteEndpoints.Add(endpoint);
        await db.SaveChangesAsync(ct);

        // Re-resolve after registration window if MeshCentral already knows the hostname.
        if (!endpoint.HasEngineNode)
        {
            string? resolved = await enrollmentEngine.TryResolveOrProvisionNodeAsync(
                endpoint.Id, request.DeviceName, null, ct);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                endpoint.MarkReady(resolved, clock.UtcNow);
                await db.SaveChangesAsync(ct);
            }
        }

        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.RemoteSession,
            AggregateId = session.Id,
            BusinessNumber = session.RemoteNumber,
            Action = BusinessAuditAction.Linked,
            FieldName = "RemoteEnrollmentRedeemed",
            NewValue = endpoint.Id.ToString("N"),
            Source = AuditSource.Api,
        }, ct);
        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.RemoteSession,
            AggregateId = session.Id,
            BusinessNumber = session.RemoteNumber,
            Action = BusinessAuditAction.Created,
            FieldName = "RemoteEndpointRegistered",
            NewValue = $"{endpoint.DeviceName}|{endpoint.OperatingSystem}",
            Source = AuditSource.Api,
        }, ct);

        logger.LogInformation(
            "Remote endpoint registered for session {RemoteNumber} (engineNode={HasNode})",
            session.RemoteNumber,
            endpoint.HasEngineNode);

        AgentBootstrapInfo bootstrap = enrollmentEngine.GetAgentBootstrap();
        string reportSecret = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        ReportSecrets[endpoint.Id] = reportSecret;

        return new EnrollmentRedeemResult(
            endpoint.Id,
            session.Id,
            endpoint.DeviceName,
            endpoint.ConnectionStatus.ToString(),
            !endpoint.IsReadyForRemote,
            bootstrap.AgentDownloadUrl,
            bootstrap.AgentInstallInstructions,
            bootstrap.AgentDownloadUrl,
            reportSecret);
    }

    public async Task<IReadOnlyList<RemoteEndpointDto>> ListForSessionAsync(
        Guid sessionId,
        Guid? ownerUserId,
        CancellationToken ct)
    {
        RemoteSessionRequest session = await db.RemoteSessionRequests.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Remote session not found.");

        Guid owner = ownerUserId ?? session.TargetUserId ?? session.RequestedByUserId;
        // Tracked entities so presence sync persists.
        List<RemoteEndpoint> items = await db.RemoteEndpoints
            .Where(x => x.ConnectionStatus != RemoteEndpointConnectionStatus.Expired
                && (x.CurrentRemoteSessionRequestId == sessionId
                    || (x.OwnerUserId == owner && x.EndpointKind == RemoteEndpointKind.Temporary)
                    || (x.OwnerUserId == owner && x.EndpointKind == RemoteEndpointKind.Managed)))
            .OrderByDescending(x => x.LastSeenAtUtc)
            .ToListAsync(ct);

        bool dirty = false;
        foreach (RemoteEndpoint endpoint in items)
        {
            if (!endpoint.HasEngineNode)
            {
                string? resolved = await enrollmentEngine.TryResolveOrProvisionNodeAsync(
                    endpoint.Id, endpoint.DeviceName, null, ct);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    endpoint.MarkReady(resolved, clock.UtcNow);
                    dirty = true;
                }
            }
            else
            {
                await enrollmentEngine.SynchronizePresenceAsync(endpoint, ct);
                dirty = true;
            }
        }

        if (dirty)
            await db.SaveChangesAsync(ct);

        return items
            .OrderByDescending(x => x.IsReadyForRemote)
            .ThenByDescending(x => x.LastSeenAtUtc)
            .Select(Map)
            .ToList();
    }

    public async Task BindEndpointToSessionAsync(
        Guid sessionId,
        Guid endpointId,
        Guid actorUserId,
        bool actorIsSupport,
        CancellationToken ct)
    {
        RemoteSessionRequest session = await db.RemoteSessionRequests
            .FirstOrDefaultAsync(x => x.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Remote session not found.");
        RemoteEndpoint endpoint = await db.RemoteEndpoints
            .FirstOrDefaultAsync(x => x.Id == endpointId, ct)
            ?? throw new InvalidOperationException("Endpoint not found.");

        if (!actorIsSupport && session.TargetUserId != actorUserId)
            throw new InvalidOperationException("Forbidden.");
        if (endpoint.OwnerUserId != session.TargetUserId && endpoint.OwnerUserId != session.RequestedByUserId)
            throw new InvalidOperationException("Endpoint does not belong to this employee.");

        DateTimeOffset now = clock.UtcNow;
        endpoint.BindSession(sessionId, now);
        session.BindRemoteEndpoint(endpointId, now);
        if (endpoint.ConfigurationItemId is Guid ci)
            session.BindConfigurationItem(ci, now);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReportEndpointStatusAsync(
        Guid endpointId,
        string? engineNodeId,
        string? connectionStatus,
        string? agentVersion,
        CancellationToken ct)
    {
        RemoteEndpoint endpoint = await db.RemoteEndpoints.FirstOrDefaultAsync(x => x.Id == endpointId, ct)
            ?? throw new InvalidOperationException("Endpoint not found.");
        DateTimeOffset now = clock.UtcNow;
        if (!string.IsNullOrWhiteSpace(agentVersion))
            endpoint.TouchHeartbeat(now);

        // Never mark Ready from helper-reported node alone — confirm via MeshCentral.
        string? confirmed = await enrollmentEngine.TryResolveOrProvisionNodeAsync(
            endpoint.Id,
            endpoint.DeviceName,
            engineNodeId,
            ct);
        if (!string.IsNullOrWhiteSpace(confirmed))
        {
            endpoint.MarkReady(confirmed, now);
        }
        else if (!string.IsNullOrWhiteSpace(connectionStatus)
                 && !string.Equals(connectionStatus, "Ready", StringComparison.OrdinalIgnoreCase)
                 && !string.Equals(connectionStatus, "Online", StringComparison.OrdinalIgnoreCase))
        {
            endpoint.TouchHeartbeat(now, connectionStatus);
        }
        else
        {
            endpoint.TouchHeartbeat(now);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Polling: resolve waiting agents and refresh presence for active temporary/managed endpoints.
    /// </summary>
    public async Task SynchronizeActiveEndpointsAsync(CancellationToken ct)
    {
        List<RemoteEndpoint> items = await db.RemoteEndpoints
            .Where(x => x.ConnectionStatus != RemoteEndpointConnectionStatus.Expired
                && x.ConnectionStatus != RemoteEndpointConnectionStatus.Failed
                && x.CurrentRemoteSessionRequestId != null)
            .Take(50)
            .ToListAsync(ct);

        foreach (RemoteEndpoint endpoint in items)
        {
            if (!endpoint.HasEngineNode)
            {
                string? resolved = await enrollmentEngine.TryResolveOrProvisionNodeAsync(
                    endpoint.Id, endpoint.DeviceName, null, ct);
                if (!string.IsNullOrWhiteSpace(resolved))
                    endpoint.MarkReady(resolved, clock.UtcNow);
            }
            else
            {
                await enrollmentEngine.SynchronizePresenceAsync(endpoint, ct);
            }
        }

        if (items.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    public async Task<RemoteEndpointDto> AttachManagedDeviceAsync(
        Guid sessionId,
        Guid employeeUserId,
        Guid configurationItemId,
        string deviceLabel,
        string? engineNodeId,
        CancellationToken ct)
    {
        RemoteSessionRequest session = await db.RemoteSessionRequests
            .FirstOrDefaultAsync(x => x.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Remote session not found.");
        if (session.TargetUserId != employeeUserId)
            throw new InvalidOperationException("Only the employee on this request may attach a device.");

        DateTimeOffset now = clock.UtcNow;
        session.BindConfigurationItem(configurationItemId, now);

        RemoteEndpoint endpoint = RemoteEndpoint.CreateManagedProjection(
            employeeUserId,
            configurationItemId,
            deviceLabel,
            engineNodeId,
            now,
            sessionId);
        session.BindRemoteEndpoint(endpoint.Id, now);
        db.RemoteEndpoints.Add(endpoint);
        await db.SaveChangesAsync(ct);
        return Map(endpoint);
    }

    public async Task<RemoteEndpointDto> LinkEndpointToCiAsync(
        Guid endpointId,
        Guid configurationItemId,
        CancellationToken ct)
    {
        RemoteEndpoint endpoint = await db.RemoteEndpoints.FirstOrDefaultAsync(x => x.Id == endpointId, ct)
            ?? throw new InvalidOperationException("Endpoint not found.");
        endpoint.LinkToConfigurationItem(configurationItemId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.RemoteSession,
            AggregateId = endpoint.CurrentRemoteSessionRequestId ?? endpoint.Id,
            Action = BusinessAuditAction.Linked,
            FieldName = "RemoteEndpointLinkedToCi",
            NewValue = configurationItemId.ToString("N"),
            Source = AuditSource.Api,
        }, ct);
        return Map(endpoint);
    }

    public async Task ExpireEndpointAsync(Guid endpointId, CancellationToken ct)
    {
        RemoteEndpoint endpoint = await db.RemoteEndpoints.FirstOrDefaultAsync(x => x.Id == endpointId, ct)
            ?? throw new InvalidOperationException("Endpoint not found.");
        endpoint.MarkExpired(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.RemoteSession,
            AggregateId = endpoint.CurrentRemoteSessionRequestId ?? endpoint.Id,
            Action = BusinessAuditAction.StatusChanged,
            FieldName = "RemoteEndpointExpired",
            NewValue = "Expired",
            Source = AuditSource.Api,
        }, ct);
    }

    public async Task ExpireDueTemporaryEndpointsAsync(CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        List<RemoteEndpoint> due = await db.RemoteEndpoints
            .Where(x => x.EndpointKind == RemoteEndpointKind.Temporary
                && x.ConnectionStatus != RemoteEndpointConnectionStatus.Expired
                && x.ExpiresAtUtc != null
                && x.ExpiresAtUtc < now)
            .ToListAsync(ct);
        foreach (RemoteEndpoint endpoint in due)
            endpoint.MarkExpired(now);
        if (due.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private static void EnforceRedeemRateLimit(string key)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        (int Count, DateTimeOffset WindowStart) state = RedeemAttempts.AddOrUpdate(
            key,
            _ => (1, now),
            (_, prev) =>
            {
                if (now - prev.WindowStart > TimeSpan.FromMinutes(5))
                    return (1, now);
                return (prev.Count + 1, prev.WindowStart);
            });
        if (state.Count > 30)
            throw new InvalidOperationException("Too many enrollment attempts. Try again later.");
    }

    private static bool CryptographicEquals(string a, string b)
    {
        byte[] left = System.Text.Encoding.UTF8.GetBytes(a);
        byte[] right = System.Text.Encoding.UTF8.GetBytes(b);
        return left.Length == right.Length
            && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static RemoteEndpointDto Map(RemoteEndpoint x) =>
        new(
            x.Id,
            x.OwnerUserId,
            x.CurrentRemoteSessionRequestId,
            x.ConfigurationItemId,
            // IT may see engine node; employee UI must hide it — DTO includes it for IT mapping only.
            x.EngineNodeId,
            x.EndpointKind.ToString(),
            x.DeviceName,
            x.OperatingSystem,
            x.OperatingSystemVersion,
            x.Architecture,
            x.HelperVersion,
            x.AgentVersion,
            x.ConnectionStatus.ToString(),
            x.IsReadyForRemote,
            x.HasEngineNode,
            x.FirstSeenAtUtc,
            x.LastSeenAtUtc,
            x.ExpiresAtUtc,
            x.CreatedAtUtc,
            x.UpdatedAtUtc,
            Convert.ToBase64String(x.RowVersion));
}
