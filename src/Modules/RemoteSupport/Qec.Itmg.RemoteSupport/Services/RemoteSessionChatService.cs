using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.RemoteSupport.Domain;
using Qec.Itmg.RemoteSupport.Persistence;

namespace Qec.Itmg.RemoteSupport.Services;

public sealed record RemoteSessionMessageDto(
    Guid Id,
    Guid RemoteSessionRequestId,
    Guid? SenderUserId,
    string MessageText,
    string MessageType,
    string? SystemEventKey,
    DateTimeOffset SentAtUtc);

public interface IRemoteSupportChatNotifier
{
    Task MessageAddedAsync(RemoteSessionMessageDto message, CancellationToken cancellationToken = default);
}

public sealed class NoOpRemoteSupportChatNotifier : IRemoteSupportChatNotifier
{
    public Task MessageAddedAsync(RemoteSessionMessageDto message, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class RemoteSessionChatService(
    RemoteSupportDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    IRemoteSupportChatNotifier chatNotifier,
    IOptions<RemoteSupportOptions> options)
{
    public static class SystemEvents
    {
        public const string Requested = "remote.requested";
        public const string SelfRequested = "remote.self_requested";
        public const string TechnicianJoined = "remote.technician_joined";
        public const string EnrollmentIssued = "remote.enrollment_issued";
        public const string HelperDownloaded = "remote.helper_downloaded";
        public const string DeviceRegistered = "remote.device_registered";
        public const string DeviceOnline = "remote.device_online";
        public const string AgentPreparing = "remote.agent_preparing";
        public const string DeviceReady = "remote.device_ready";
        public const string AccessRequested = "remote.access_requested";
        public const string Allowed = "remote.allowed";
        public const string Declined = "remote.declined";
        public const string Expired = "remote.expired";
        public const string Connecting = "remote.connecting";
        public const string Started = "remote.started";
        public const string Ended = "remote.ended";
        public const string Failed = "remote.failed";
        public const string ChatOpened = "remote.chat_opened";
        public const string DeviceExpired = "remote.device_expired";
    }

    public async Task<IReadOnlyList<RemoteSessionMessageDto>> ListAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        List<RemoteSessionMessage> items = await db.RemoteSessionMessages.AsNoTracking()
            .Where(x => x.RemoteSessionRequestId == sessionId)
            .OrderBy(x => x.SentAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<RemoteSessionMessageDto> PostUserMessageAsync(
        Guid sessionId,
        Guid senderUserId,
        string messageText,
        CancellationToken cancellationToken = default)
    {
        RemoteSessionRequest session = await LoadSessionAsync(sessionId, cancellationToken);
        EnsureChatWindowOpen(session);

        RemoteSessionMessage message = RemoteSessionMessage.CreateUser(
            sessionId, senderUserId, messageText, clock.UtcNow);
        db.RemoteSessionMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);

        RemoteSessionMessageDto dto = Map(message);
        await chatNotifier.MessageAddedAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<RemoteSessionMessageDto> PostSystemMessageAsync(
        Guid sessionId,
        string systemEventKey,
        string messageText,
        CancellationToken cancellationToken = default)
    {
        _ = await LoadSessionAsync(sessionId, cancellationToken);
        RemoteSessionMessage message = RemoteSessionMessage.CreateSystem(
            sessionId, systemEventKey, messageText, clock.UtcNow);
        db.RemoteSessionMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);

        RemoteSessionMessageDto dto = Map(message);
        await chatNotifier.MessageAddedAsync(dto, cancellationToken);
        return dto;
    }

    public async Task EnsureChatStartedAuditAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        // System lifecycle messages may already exist; audit first human message only.
        bool hasUserMessage = await db.RemoteSessionMessages.AsNoTracking()
            .AnyAsync(
                x => x.RemoteSessionRequestId == sessionId
                    && x.MessageType == RemoteSessionMessageType.User,
                cancellationToken);
        if (hasUserMessage) return;

        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.RemoteSession,
            AggregateId = sessionId,
            Action = BusinessAuditAction.Updated,
            FieldName = "RemoteChatStarted",
            NewValue = "true",
            Source = AuditSource.Api,
        }, cancellationToken);
    }

    public bool AgentDownloadConfigured => options.Value.HasAgentDownload;

    public (string? DownloadUrl, string? Instructions) GetAgentOnboarding()
    {
        RemoteSupportOptions cfg = options.Value;
        return (
            cfg.HasAgentDownload ? cfg.AgentDownloadUrl.Trim() : null,
            string.IsNullOrWhiteSpace(cfg.AgentInstallInstructions) ? null : cfg.AgentInstallInstructions.Trim());
    }

    private async Task<RemoteSessionRequest> LoadSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await db.RemoteSessionRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
        ?? throw new InvalidOperationException("Remote session not found.");

    private static void EnsureChatWindowOpen(RemoteSessionRequest session)
    {
        // Chat stays open from request creation through a short post-end window (7 days).
        if (session.Status == RemoteSessionStatus.Ended
            && session.EndedAtUtc is DateTimeOffset ended
            && ended < DateTimeOffset.UtcNow.AddDays(-7))
        {
            throw new InvalidOperationException("Chat is closed for this remote session.");
        }
    }

    private static RemoteSessionMessageDto Map(RemoteSessionMessage x) =>
        new(x.Id, x.RemoteSessionRequestId, x.SenderUserId, x.MessageText, x.MessageType.ToString(),
            x.SystemEventKey, x.SentAtUtc);
}
