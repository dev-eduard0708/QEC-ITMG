using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qec.Itmg.BuildingBlocks.Email;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Notifications;
using Qec.Itmg.RemoteSupport.Services;

namespace Qec.Itmg.Host.RemoteSupport;

public sealed class RemoteSupportNotificationService(
    INotificationService notifications,
    IEmailQueue emailQueue,
    IdentityDbContext identityDb,
    ILogger<RemoteSupportNotificationService> logger)
{
    public const string ResourceType = "RemoteSession";

    public Task NotifyRequestedAsync(RemoteSessionRequestDto request, CancellationToken ct) =>
        request.TargetUserId is Guid target
            ? NotifyUserAsync(
                target,
                "remote.requested",
                NotificationSeverity.Warning,
                $"Remote support requested ({request.RemoteNumber})",
                $"A technician requested remote access. Reason: {Truncate(request.Reason)}",
                request.Id,
                EmployeeActionUrl(request.Id),
                ct)
            : Task.CompletedTask;

    public Task NotifyAllowedAsync(RemoteSessionRequestDto request, CancellationToken ct) =>
        request.TechnicianUserId is Guid tech
            ? NotifyUserAsync(
                tech,
                "remote.allowed",
                NotificationSeverity.Info,
                $"{request.RemoteNumber} allowed",
                "The employee allowed the remote support request.",
                request.Id,
                ItActionUrl(request.Id),
                ct)
            : Task.CompletedTask;

    public Task NotifyDeclinedAsync(RemoteSessionRequestDto request, CancellationToken ct) =>
        request.TechnicianUserId is Guid tech
            ? NotifyUserAsync(
                tech,
                "remote.declined",
                NotificationSeverity.Warning,
                $"{request.RemoteNumber} declined",
                "The employee declined the remote support request.",
                request.Id,
                ItActionUrl(request.Id),
                ct)
            : Task.CompletedTask;

    public Task NotifyExpiredAsync(RemoteSessionRequestDto request, CancellationToken ct) =>
        request.TechnicianUserId is Guid tech
            ? NotifyUserAsync(
                tech,
                "remote.expired",
                NotificationSeverity.Info,
                $"{request.RemoteNumber} expired",
                "The remote support consent window expired.",
                request.Id,
                ItActionUrl(request.Id),
                ct)
            : Task.CompletedTask;

    public Task NotifySessionStartedAsync(RemoteSessionRequestDto request, CancellationToken ct) =>
        request.TargetUserId is Guid target
            ? NotifyUserAsync(
                target,
                "remote.started",
                NotificationSeverity.Warning,
                $"{request.RemoteNumber} session started",
                "A remote support session is now active on your device.",
                request.Id,
                EmployeeActionUrl(request.Id),
                ct)
            : Task.CompletedTask;

    public async Task NotifySessionEndedAsync(RemoteSessionRequestDto request, CancellationToken ct)
    {
        if (request.TargetUserId is Guid target)
        {
            await NotifyUserAsync(
                target,
                "remote.ended",
                NotificationSeverity.Info,
                $"{request.RemoteNumber} session ended",
                "The remote support session has ended.",
                request.Id,
                EmployeeActionUrl(request.Id),
                ct);
        }

        if (request.TechnicianUserId is Guid tech && tech != request.TargetUserId)
        {
            await NotifyUserAsync(
                tech,
                "remote.ended",
                NotificationSeverity.Info,
                $"{request.RemoteNumber} session ended",
                request.Outcome is null
                    ? "The remote support session has ended."
                    : $"Session ended ({request.Outcome}).",
                request.Id,
                ItActionUrl(request.Id),
                ct);
        }
    }

    public Task NotifyEngineFailedAsync(RemoteSessionRequestDto request, string error, CancellationToken ct) =>
        request.TechnicianUserId is Guid tech
            ? NotifyUserAsync(
                tech,
                "remote.engine_failed",
                NotificationSeverity.Critical,
                $"{request.RemoteNumber} connection failed",
                Truncate(error),
                request.Id,
                ItActionUrl(request.Id),
                ct)
            : Task.CompletedTask;

    /// <summary>In-app only — never email chat traffic.</summary>
    public async Task NotifyChatMessageAsync(
        RemoteSessionRequestDto request,
        Guid senderUserId,
        string messagePreview,
        CancellationToken ct)
    {
        Guid? recipient = null;
        string actionUrl;
        if (request.TargetUserId is Guid target && target != senderUserId)
        {
            recipient = target;
            actionUrl = EmployeeActionUrl(request.Id);
        }
        else if (request.TechnicianUserId is Guid tech && tech != senderUserId)
        {
            recipient = tech;
            actionUrl = ItActionUrl(request.Id);
        }
        else if (request.RequestedByUserId != senderUserId)
        {
            recipient = request.RequestedByUserId;
            actionUrl = ItActionUrl(request.Id);
        }
        else
        {
            return;
        }

        await NotifyInAppOnlyAsync(
            recipient.Value,
            "remote.chat",
            NotificationSeverity.Info,
            $"{request.RemoteNumber} new message",
            Truncate(messagePreview),
            request.Id,
            actionUrl,
            ct);
    }

    private async Task NotifyUserAsync(
        Guid recipientUserId,
        string type,
        NotificationSeverity severity,
        string title,
        string message,
        Guid resourceId,
        string actionUrl,
        CancellationToken cancellationToken)
    {
        bool created = await NotifyInAppOnlyAsync(
            recipientUserId, type, severity, title, message, resourceId, actionUrl, cancellationToken);
        if (!created) return;

        try
        {
            string? email = await identityDb.Users.AsNoTracking()
                .Where(user => user.Id == recipientUserId && user.Status == UserStatus.Active)
                .Select(user => user.Upn)
                .FirstOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
                return;

            emailQueue.Enqueue(new EmailMessage
            {
                To = email,
                Subject = title,
                BodyText = $"{message}\n\nOpen: {actionUrl}",
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enqueue remote support email for user {UserId}", recipientUserId);
        }
    }

    private async Task<bool> NotifyInAppOnlyAsync(
        Guid recipientUserId,
        string type,
        NotificationSeverity severity,
        string title,
        string message,
        Guid resourceId,
        string actionUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            await notifications.CreateAsync(
                recipientUserId,
                type,
                severity,
                title,
                message,
                ResourceType,
                resourceId,
                actionUrl,
                cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Remote support notification {Type} failed for user {UserId}", type, recipientUserId);
            return false;
        }
    }

    private static string EmployeeActionUrl(Guid id) => $"/employee/remote-support/{id:D}";
    private static string ItActionUrl(Guid id) => $"/it/remote-support/{id:D}";
    private static string Truncate(string value) =>
        value.Length <= 240 ? value : value[..237] + "...";
}
