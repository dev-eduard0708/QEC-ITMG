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
    string? AgentInstallInstructions);

public sealed record EnrollmentRedeemRequest(
    string Token,
    string DeviceName,
    string OperatingSystem,
    string? OperatingSystemVersion,
    string? Architecture,
    string? HelperVersion,
    string? ReportedEngineNodeId);

public sealed record EnrollmentRedeemResult(
    Guid EndpointId,
    Guid RemoteSessionRequestId,
    string DeviceName,
    string ConnectionStatus,
    bool WaitingForRemoteAgent,
    string? AgentDownloadUrl,
    string? AgentInstallInstructions);

/// <summary>
/// Optional MeshCentral enrollment hooks. Current MeshCentral adapter does not expose
/// documented agent-provision APIs — implementations may return deferred.
/// </summary>
public interface IRemoteEndpointEnrollmentEngine
{
    Task<string?> TryResolveOrProvisionNodeAsync(
        Guid endpointId,
        string deviceName,
        string? reportedEngineNodeId,
        CancellationToken cancellationToken = default);
}

public sealed class DeferredRemoteEndpointEnrollmentEngine : IRemoteEndpointEnrollmentEngine
{
    public Task<string?> TryResolveOrProvisionNodeAsync(
        Guid endpointId,
        string deviceName,
        string? reportedEngineNodeId,
        CancellationToken cancellationToken = default)
    {
        // Prefer an explicitly reported node id from a configured agent install path;
        // never invent MeshCentral node identifiers.
        if (!string.IsNullOrWhiteSpace(reportedEngineNodeId))
            return Task.FromResult<string?>(reportedEngineNodeId.Trim());
        return Task.FromResult<string?>(null);
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
        return new EnrollmentIssueResult(
            enrollment.Id,
            plain,
            enrollment.ExpiresAtUtc,
            cfg.HasHelperDownload,
            cfg.HasHelperDownload ? cfg.HelperDownloadUrl.Trim() : null,
            string.IsNullOrWhiteSpace(cfg.HelperInstallInstructions) ? null : cfg.HelperInstallInstructions.Trim(),
            cfg.HasAgentDownload,
            cfg.HasAgentDownload ? cfg.AgentDownloadUrl.Trim() : null,
            string.IsNullOrWhiteSpace(cfg.AgentInstallInstructions) ? null : cfg.AgentInstallInstructions.Trim());
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

        enrollment.Redeem(endpoint.Id, now);
        session.BindRemoteEndpoint(endpoint.Id, now);
        db.RemoteEndpoints.Add(endpoint);
        await db.SaveChangesAsync(ct);

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

        RemoteSupportOptions cfg = options.Value;
        return new EnrollmentRedeemResult(
            endpoint.Id,
            session.Id,
            endpoint.DeviceName,
            endpoint.ConnectionStatus.ToString(),
            !endpoint.HasEngineNode,
            cfg.HasAgentDownload ? cfg.AgentDownloadUrl.Trim() : null,
            string.IsNullOrWhiteSpace(cfg.AgentInstallInstructions) ? null : cfg.AgentInstallInstructions.Trim());
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
