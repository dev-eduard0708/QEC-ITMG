using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.Contracts.RemoteSupport;
using Qec.Itmg.RemoteSupport.Domain;
using Qec.Itmg.RemoteSupport.Persistence;

namespace Qec.Itmg.RemoteSupport.Services;

public sealed record RemoteSessionRequestDto(
    Guid Id,
    string RemoteNumber,
    Guid ConfigurationItemId,
    Guid? TicketId,
    Guid? ChangeRequestId,
    Guid RequestedByUserId,
    Guid? TargetUserId,
    Guid? TechnicianUserId,
    string Reason,
    string? RequestedPrivileges,
    string SessionType,
    string Status,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? AllowedAtUtc,
    DateTimeOffset? DeclinedAtUtc,
    DateTimeOffset? ConnectingAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    string? EngineSessionId,
    string? EngineJoinUrl,
    string? Outcome,
    string? EndReason,
    Guid? ConsentUserId,
    string? ConsentIpAddress,
    bool? ElevationUsed,
    string? RecordingReference,
    string? LastEngineError,
    bool MfaSatisfiedAtStart,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string RowVersion,
    int? DurationSeconds);

public sealed record RemoteSessionListResult(
    IReadOnlyList<RemoteSessionRequestDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

internal static class RemoteAudit
{
    public static BusinessAuditEntry Created(Guid id, string number) => new()
    {
        AggregateType = AuditAggregateType.RemoteSession,
        AggregateId = id,
        BusinessNumber = number,
        Action = BusinessAuditAction.Created,
        Source = AuditSource.Api,
    };

    public static BusinessAuditEntry Field(
        Guid id,
        string? number,
        string field,
        string? oldValue,
        string? newValue,
        BusinessAuditAction action = BusinessAuditAction.StatusChanged,
        string? reason = null) => new()
    {
        AggregateType = AuditAggregateType.RemoteSession,
        AggregateId = id,
        BusinessNumber = number,
        Action = action,
        FieldName = field,
        OldValue = oldValue,
        NewValue = newValue,
        Reason = reason,
        Source = AuditSource.Api,
    };
}

public sealed class RemoteSessionService(
    RemoteSupportDbContext db,
    INumberSequenceService numbers,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction,
    IRemoteSupportEngine engine,
    IRemoteCiLookup ciLookup,
    IOptions<RemoteSupportOptions> options)
{
    public const string SequenceKey = "remote-sessions";
    public const string Prefix = "REM";

    public RemoteEngineStatus GetEngineStatus() => engine.GetStatus();

    public async Task<RemoteSessionListResult> ListAsync(
        int page,
        int pageSize,
        string? status,
        Guid? targetUserId,
        Guid? technicianUserId,
        Guid? ticketId,
        Guid? configurationItemId,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<RemoteSessionRequest> q = db.RemoteSessionRequests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse(status, true, out RemoteSessionStatus st))
        {
            q = q.Where(x => x.Status == st);
        }

        if (targetUserId is Guid t) q = q.Where(x => x.TargetUserId == t);
        if (technicianUserId is Guid tech) q = q.Where(x => x.TechnicianUserId == tech);
        if (ticketId is Guid tk) q = q.Where(x => x.TicketId == tk);
        if (configurationItemId is Guid ci) q = q.Where(x => x.ConfigurationItemId == ci);

        int total = await q.CountAsync(ct);
        List<RemoteSessionRequest> items = await q
            .OrderByDescending(x => x.RequestedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return new(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<RemoteSessionRequestDto?> GetAsync(Guid id, CancellationToken ct)
    {
        RemoteSessionRequest? entity = await db.RemoteSessionRequests.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return entity is null ? null : Map(entity);
    }

    public async Task<RemoteSessionRequestDto> CreateAttendedAsync(
        Guid configurationItemId,
        Guid requestedByUserId,
        Guid targetUserId,
        string reason,
        Guid? ticketId,
        Guid? changeRequestId,
        string? requestedPrivileges,
        Guid? technicianUserId,
        CancellationToken ct)
    {
        await EnsureCiExistsAsync(configurationItemId, ct);
        if (ticketId is null && changeRequestId is null)
            throw new InvalidOperationException("Attended remote support normally requires a linked ticket or change.");

        string number = await numbers.NextAsync(SequenceKey, Prefix, ct);
        RemoteSessionRequest entity = RemoteSessionRequest.CreateAttended(
            number,
            configurationItemId,
            requestedByUserId,
            targetUserId,
            reason,
            clock.UtcNow,
            TimeSpan.FromMinutes(Math.Max(5, options.Value.DefaultConsentExpiryMinutes)),
            ticketId,
            changeRequestId,
            requestedPrivileges,
            technicianUserId);

        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            db.RemoteSessionRequests.Add(entity);
            await db.SaveChangesAsync(innerCt);
            await businessAudit.AppendAsync(RemoteAudit.Created(entity.Id, entity.RemoteNumber), innerCt);
            await businessAudit.AppendAsync(
                RemoteAudit.Field(entity.Id, entity.RemoteNumber, "Status", null, entity.Status.ToString()), innerCt);
        }, ct);

        return Map(entity);
    }

    public async Task<RemoteSessionRequestDto> CreateUnattendedAsync(
        Guid configurationItemId,
        Guid requestedByUserId,
        string reason,
        Guid? ticketId,
        Guid? changeRequestId,
        string? requestedPrivileges,
        Guid? technicianUserId,
        bool mfaSatisfied,
        CancellationToken ct)
    {
        RemoteSupportOptions opts = options.Value;
        if (!opts.UnattendedEnabled)
            throw new InvalidOperationException("Unattended remote support is disabled by policy (default OFF).");

        RemoteCiProjection ci = await EnsureCiExistsAsync(configurationItemId, ct);
        ValidateUnattendedPolicy(ci, ticketId, changeRequestId, mfaSatisfied, opts);

        string number = await numbers.NextAsync(SequenceKey, Prefix, ct);
        RemoteSessionRequest entity = RemoteSessionRequest.CreateUnattended(
            number,
            configurationItemId,
            requestedByUserId,
            reason,
            clock.UtcNow,
            ticketId,
            changeRequestId,
            requestedPrivileges,
            technicianUserId);

        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            db.RemoteSessionRequests.Add(entity);
            await db.SaveChangesAsync(innerCt);
            await businessAudit.AppendAsync(RemoteAudit.Created(entity.Id, entity.RemoteNumber), innerCt);
            await businessAudit.AppendAsync(
                RemoteAudit.Field(
                    entity.Id,
                    entity.RemoteNumber,
                    "UnattendedAuthorized",
                    null,
                    "true",
                    BusinessAuditAction.Created,
                    reason), innerCt);
        }, ct);

        return Map(entity);
    }

    public async Task<RemoteSessionRequestDto> AllowAsync(
        Guid id,
        Guid consentUserId,
        string? ipAddress,
        CancellationToken ct)
    {
        RemoteSessionRequest entity = await LoadTrackedAsync(id, ct);
        string previous = entity.Status.ToString();
        entity.Allow(consentUserId, ipAddress, clock.UtcNow);
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            await db.SaveChangesAsync(innerCt);
            await businessAudit.AppendAsync(
                RemoteAudit.Field(entity.Id, entity.RemoteNumber, "Status", previous, entity.Status.ToString(), reason: "Consent allowed"), innerCt);
            await businessAudit.AppendAsync(
                RemoteAudit.Field(entity.Id, entity.RemoteNumber, "ConsentUserId", null, consentUserId.ToString("D"), BusinessAuditAction.Updated), innerCt);
        }, ct);
        return Map(entity);
    }

    public async Task<RemoteSessionRequestDto> DeclineAsync(
        Guid id,
        Guid consentUserId,
        string? ipAddress,
        CancellationToken ct)
    {
        RemoteSessionRequest entity = await LoadTrackedAsync(id, ct);
        string previous = entity.Status.ToString();
        entity.Decline(consentUserId, ipAddress, clock.UtcNow);
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            await db.SaveChangesAsync(innerCt);
            await businessAudit.AppendAsync(
                RemoteAudit.Field(entity.Id, entity.RemoteNumber, "Status", previous, entity.Status.ToString(), reason: "Consent declined"), innerCt);
        }, ct);
        return Map(entity);
    }

    public async Task<RemoteSessionRequestDto> StartAsync(
        Guid id,
        Guid actorUserId,
        bool actorHasAttended,
        bool actorHasUnattended,
        bool mfaSatisfied,
        CancellationToken ct)
    {
        RemoteSessionRequest entity = await LoadTrackedAsync(id, ct);
        RemoteCiProjection ci = await EnsureCiExistsAsync(entity.ConfigurationItemId, ct);

        if (string.IsNullOrWhiteSpace(ci.RemoteEngineNodeId))
            throw new InvalidOperationException("Configuration item has no RemoteEngineNodeId mapping; connection cannot start.");

        if (entity.SessionType == RemoteSessionType.Attended)
        {
            if (!actorHasAttended)
                throw new InvalidOperationException("remote.attended permission is required to start attended sessions.");
            if (entity.Status != RemoteSessionStatus.Allowed)
                throw new InvalidOperationException("Attended session requires Allowed consent before connect.");
            if (entity.ExpiresAtUtc is DateTimeOffset exp && clock.UtcNow > exp)
            {
                entity.Expire(clock.UtcNow);
                await db.SaveChangesAsync(ct);
                throw new InvalidOperationException("Remote support request has expired.");
            }
        }
        else
        {
            if (!actorHasUnattended)
                throw new InvalidOperationException("remote.unattended permission is required.");
            ValidateUnattendedPolicy(ci, entity.TicketId, entity.ChangeRequestId, mfaSatisfied, options.Value);
        }

        if (entity.TechnicianUserId is Guid tech && tech != actorUserId && entity.RequestedByUserId != actorUserId)
            throw new InvalidOperationException("Only the assigned technician may start this session.");

        RemoteEngineStatus engineStatus = engine.GetStatus();
        if (!engineStatus.Enabled || !engineStatus.Configured)
            throw new InvalidOperationException("Remote support engine unavailable");

        string previous = entity.Status.ToString();
        entity.BeginConnecting(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            RemoteAudit.Field(entity.Id, entity.RemoteNumber, "Status", previous, entity.Status.ToString(), reason: "Start attempted"), ct);

        var engineRequest = new CreateRemoteEngineSessionRequest(
            entity.Id,
            entity.RemoteNumber,
            ci.RemoteEngineNodeId!,
            entity.SessionType.ToString(),
            entity.TechnicianUserId ?? actorUserId,
            entity.TargetUserId,
            entity.Reason,
            entity.RequestedPrivileges,
            entity.SessionType == RemoteSessionType.Unattended);

        RemoteEngineSessionResult result = entity.SessionType == RemoteSessionType.Unattended
            ? await engine.CreateUnattendedSessionAsync(engineRequest, ct)
            : await engine.CreateAttendedSessionAsync(engineRequest, ct);

        if (!result.Success || string.IsNullOrWhiteSpace(result.EngineSessionId))
        {
            entity.MarkConnectFailed(result.ErrorSummary ?? "Engine failure", clock.UtcNow);
            await sharedDbTransaction.ExecuteAsync(async innerCt =>
            {
                await db.SaveChangesAsync(innerCt);
                await businessAudit.AppendAsync(
                    RemoteAudit.Field(
                        entity.Id,
                        entity.RemoteNumber,
                        "Status",
                        RemoteSessionStatus.Connecting.ToString(),
                        entity.Status.ToString(),
                        reason: result.ErrorSummary ?? "Engine failure"), innerCt);
            }, ct);
            throw new InvalidOperationException(result.ErrorSummary ?? "Remote support engine unavailable");
        }

        previous = entity.Status.ToString();
        entity.MarkInSession(result.EngineSessionId, result.JoinUrl, mfaSatisfied, clock.UtcNow);
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            await db.SaveChangesAsync(innerCt);
            await businessAudit.AppendAsync(
                RemoteAudit.Field(entity.Id, entity.RemoteNumber, "Status", previous, entity.Status.ToString(), reason: "Start success"), innerCt);
            await businessAudit.AppendAsync(
                RemoteAudit.Field(entity.Id, entity.RemoteNumber, "EngineSessionId", null, result.EngineSessionId, BusinessAuditAction.Linked), innerCt);
        }, ct);

        return Map(entity);
    }

    public async Task<RemoteSessionRequestDto> EndAsync(
        Guid id,
        Guid actorUserId,
        bool byTechnician,
        string? reason,
        CancellationToken ct)
    {
        RemoteSessionRequest entity = await LoadTrackedAsync(id, ct);
        if (entity.EngineSessionId is string engineId)
            await engine.EndSessionAsync(engineId, reason, ct);

        string previous = entity.Status.ToString();
        RemoteSessionOutcome outcome = byTechnician
            ? RemoteSessionOutcome.TerminatedByTechnician
            : RemoteSessionOutcome.TerminatedByUser;
        entity.EndByActor(outcome, reason, clock.UtcNow);
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            await db.SaveChangesAsync(innerCt);
            await businessAudit.AppendAsync(
                RemoteAudit.Field(entity.Id, entity.RemoteNumber, "Status", previous, entity.Status.ToString(), reason: reason), innerCt);
        }, ct);
        return Map(entity);
    }

    public async Task<bool> CompleteFromEngineAsync(
        string engineSessionId,
        RemoteSessionOutcome outcome,
        string? endReason,
        bool? elevationUsed,
        string? recordingReference,
        DateTimeOffset? endedAtUtc,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineSessionId);
        RemoteSessionRequest? entity = await db.RemoteSessionRequests
            .FirstOrDefaultAsync(x => x.EngineSessionId == engineSessionId.Trim(), ct);
        if (entity is null)
            return false;

        string previous = entity.Status.ToString();
        bool changed = entity.TryCompleteFromEngine(
            endedAtUtc ?? clock.UtcNow,
            outcome,
            endReason,
            elevationUsed,
            recordingReference);
        if (!changed)
            return false;

        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            await db.SaveChangesAsync(innerCt);
            await businessAudit.AppendAsync(
                RemoteAudit.Field(
                    entity.Id,
                    entity.RemoteNumber,
                    "Status",
                    previous,
                    entity.Status.ToString(),
                    reason: endReason ?? "Engine session ended",
                    action: BusinessAuditAction.StatusChanged), innerCt);
        }, ct);
        return true;
    }

    public async Task<IReadOnlyList<RemoteSessionRequestDto>> ExpireDueAsync(CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        List<RemoteSessionRequest> due = await db.RemoteSessionRequests
            .Where(x => x.SessionType == RemoteSessionType.Attended
                && (x.Status == RemoteSessionStatus.NotifyUser || x.Status == RemoteSessionStatus.Requested)
                && x.ExpiresAtUtc != null
                && x.ExpiresAtUtc < now)
            .ToListAsync(ct);

        foreach (RemoteSessionRequest entity in due)
        {
            string previous = entity.Status.ToString();
            entity.Expire(now);
            await businessAudit.AppendAsync(
                RemoteAudit.Field(entity.Id, entity.RemoteNumber, "Status", previous, entity.Status.ToString(), reason: "Expired"), ct);
        }

        if (due.Count > 0)
            await db.SaveChangesAsync(ct);

        return due.Select(Map).ToList();
    }

    public async Task PollActiveSessionsAsync(CancellationToken ct)
    {
        List<RemoteSessionRequest> active = await db.RemoteSessionRequests
            .Where(x => x.Status == RemoteSessionStatus.InSession && x.EngineSessionId != null)
            .ToListAsync(ct);

        foreach (RemoteSessionRequest entity in active)
        {
            RemoteEngineSessionInfo? info = await engine.GetSessionAsync(entity.EngineSessionId!, ct);
            if (info is null)
                continue;

            if (!string.Equals(info.Status, "ended", StringComparison.OrdinalIgnoreCase)
                && info.EndedAtUtc is null)
            {
                continue;
            }

            RemoteSessionOutcome outcome = ParseOutcome(info.Outcome) ?? RemoteSessionOutcome.Completed;
            await CompleteFromEngineAsync(
                entity.EngineSessionId!,
                outcome,
                info.EndReason,
                info.ElevationUsed,
                info.RecordingReference,
                info.EndedAtUtc,
                ct);
        }
    }

    private static void ValidateUnattendedPolicy(
        RemoteCiProjection ci,
        Guid? ticketId,
        Guid? changeRequestId,
        bool mfaSatisfied,
        RemoteSupportOptions opts)
    {
        if (!opts.UnattendedEnabled)
            throw new InvalidOperationException("Unattended remote support is disabled by policy (default OFF).");
        if (!ci.UnattendedRemotePermitted)
            throw new InvalidOperationException("CI is not tagged UnattendedRemotePermitted.");

        HashSet<string> allowedTypes = opts.UnattendedAllowedCiTypeKeys
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!allowedTypes.Contains(ci.CiTypeKey))
            throw new InvalidOperationException($"CI type '{ci.CiTypeKey}' is not allowed for unattended remote.");

        if (ticketId is null && changeRequestId is null)
            throw new InvalidOperationException("Unattended remote requires a linked ticket or change.");

        if (opts.RequireChangeForCriticalUnattended
            && string.Equals(ci.Criticality, "Critical", StringComparison.OrdinalIgnoreCase)
            && changeRequestId is null)
        {
            throw new InvalidOperationException("Critical CIs require a linked Change for unattended remote.");
        }

        if (opts.RequireMfaForUnattended && !mfaSatisfied)
            throw new InvalidOperationException("MFA/step-up is required for unattended remote.");
    }

    private async Task<RemoteCiProjection> EnsureCiExistsAsync(Guid configurationItemId, CancellationToken ct)
    {
        RemoteCiProjection? ci = await ciLookup.GetAsync(configurationItemId, ct);
        if (ci is null)
            throw new InvalidOperationException("Configuration item was not found.");
        if (!string.Equals(ci.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Configuration item must be Active.");
        return ci;
    }

    private async Task<RemoteSessionRequest> LoadTrackedAsync(Guid id, CancellationToken ct) =>
        await db.RemoteSessionRequests.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new InvalidOperationException("Remote session request was not found.");

    private static RemoteSessionOutcome? ParseOutcome(string? value) =>
        Enum.TryParse(value, true, out RemoteSessionOutcome o) ? o : null;

    private static RemoteSessionRequestDto Map(RemoteSessionRequest x)
    {
        int? duration = null;
        if (x.StartedAtUtc is DateTimeOffset start)
        {
            DateTimeOffset end = x.EndedAtUtc ?? DateTimeOffset.UtcNow;
            duration = (int)Math.Max(0, (end - start).TotalSeconds);
        }

        return new(
            x.Id,
            x.RemoteNumber,
            x.ConfigurationItemId,
            x.TicketId,
            x.ChangeRequestId,
            x.RequestedByUserId,
            x.TargetUserId,
            x.TechnicianUserId,
            x.Reason,
            x.RequestedPrivileges,
            x.SessionType.ToString(),
            x.Status.ToString(),
            x.RequestedAtUtc,
            x.ExpiresAtUtc,
            x.AllowedAtUtc,
            x.DeclinedAtUtc,
            x.ConnectingAtUtc,
            x.StartedAtUtc,
            x.EndedAtUtc,
            x.EngineSessionId,
            x.EngineJoinUrl,
            x.Outcome?.ToString(),
            x.EndReason,
            x.ConsentUserId,
            x.ConsentIpAddress,
            x.ElevationUsed,
            x.RecordingReference,
            x.LastEngineError,
            x.MfaSatisfiedAtStart,
            x.CreatedAtUtc,
            x.UpdatedAtUtc,
            Convert.ToBase64String(x.RowVersion),
            duration);
    }
}
