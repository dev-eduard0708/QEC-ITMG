using Microsoft.Extensions.Logging;
using Qec.Itmg.BuildingBlocks.Email;
using Qec.Itmg.ChangeManagement.Services;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Qec.Itmg.Host.ChangeManagement;

public sealed class ChangeNotificationService(
    INotificationService notifications,
    IEmailQueue emailQueue,
    IdentityDbContext identityDb,
    ILogger<ChangeNotificationService> logger)
{
    public const string ResourceType = "Change";

    public async Task NotifyApprovalRequestedAsync(
        ChangeDto change,
        Guid approverUserId,
        CancellationToken cancellationToken = default)
    {
        await NotifyUserAsync(
            approverUserId,
            "change.approval_requested",
            NotificationSeverity.Warning,
            $"Approval requested: {change.ChangeNumber}",
            $"Please review and approve or reject \"{change.Title}\".",
            change.Id,
            cancellationToken);
    }

    public async Task NotifyDecisionAsync(
        ChangeDto change,
        bool approved,
        CancellationToken cancellationToken = default)
    {
        string type = approved ? "change.approved" : "change.rejected";
        string title = approved
            ? $"{change.ChangeNumber} approved"
            : $"{change.ChangeNumber} rejected";
        string message = approved
            ? $"Change \"{change.Title}\" was approved."
            : $"Change \"{change.Title}\" was rejected.";

        await NotifyStakeholdersAsync(change, type, NotificationSeverity.Info, title, message, cancellationToken);
    }

    public async Task NotifyStatusAsync(
        ChangeDto change,
        string previousStatus,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(previousStatus, change.Status, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        (string type, NotificationSeverity severity, string title, string message)? payload = change.Status switch
        {
            "Scheduled" => ("change.scheduled", NotificationSeverity.Info,
                $"{change.ChangeNumber} scheduled",
                $"Change \"{change.Title}\" was scheduled."),
            "Failed" => ("change.implementation_failed", NotificationSeverity.Critical,
                $"{change.ChangeNumber} failed",
                $"Implementation failed for \"{change.Title}\"."),
            "RolledBack" => ("change.rolled_back", NotificationSeverity.Warning,
                $"{change.ChangeNumber} rolled back",
                $"Change \"{change.Title}\" was rolled back."),
            "RequiresFollowUp" => ("change.requires_follow_up", NotificationSeverity.Warning,
                $"{change.ChangeNumber} needs follow-up",
                $"Change \"{change.Title}\" requires follow-up."),
            _ => null,
        };

        if (payload is null) return;
        await NotifyStakeholdersAsync(
            change, payload.Value.type, payload.Value.severity, payload.Value.title, payload.Value.message, cancellationToken);
    }

    private async Task NotifyStakeholdersAsync(
        ChangeDto change,
        string type,
        NotificationSeverity severity,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        HashSet<Guid> recipients = [change.RequesterUserId];
        if (change.OwnerUserId is Guid owner && owner != Guid.Empty)
        {
            recipients.Add(owner);
        }

        foreach (Guid recipient in recipients)
        {
            await NotifyUserAsync(recipient, type, severity, title, message, change.Id, cancellationToken);
        }
    }

    private async Task NotifyUserAsync(
        Guid recipientUserId,
        string type,
        NotificationSeverity severity,
        string title,
        string message,
        Guid changeId,
        CancellationToken cancellationToken)
    {
        if (recipientUserId == Guid.Empty) return;

        try
        {
            await notifications.CreateAsync(
                recipientUserId,
                type,
                severity,
                title,
                message,
                ResourceType,
                changeId,
                ActionUrl(changeId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create change notification {Type} for user {UserId}", type, recipientUserId);
            return;
        }

        try
        {
            string? email = await identityDb.Users.AsNoTracking()
                .Where(user => user.Id == recipientUserId && user.Status == UserStatus.Active)
                .Select(user => user.Upn)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
            {
                return;
            }

            emailQueue.Enqueue(new EmailMessage
            {
                To = email,
                Subject = title,
                BodyText = $"{message}\n\nOpen: {ActionUrl(changeId)}",
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enqueue change email for user {UserId}", recipientUserId);
        }
    }

    private static string ActionUrl(Guid changeId) => $"/it/changes/{changeId}";
}
