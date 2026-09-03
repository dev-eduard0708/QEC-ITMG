using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qec.Itmg.BuildingBlocks.Email;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Notifications;
using Qec.Itmg.ServiceDesk.Services;

namespace Qec.Itmg.Host.ServiceDesk;

public sealed class TicketNotificationService(
    INotificationService notifications,
    IEmailQueue emailQueue,
    IdentityDbContext identityDb,
    ILogger<TicketNotificationService> logger)
{
    public const string ResourceType = "Ticket";

    public async Task NotifyTicketCreatedAsync(
        TicketDto ticket,
        CancellationToken cancellationToken = default)
    {
        await NotifyUserAsync(
            ticket.RequesterUserId,
            "ticket.created",
            NotificationSeverity.Info,
            $"Request {ticket.TicketNumber} received",
            $"Your request \"{ticket.Title}\" was created.",
            ticket.Id,
            EmployeeActionUrl(ticket.Id),
            cancellationToken);
    }

    public async Task NotifyStatusChangedAsync(
        TicketDto ticket,
        string previousStatus,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(previousStatus, ticket.Status, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string type = ticket.Status is "Resolved" or "Closed"
            ? "ticket.resolved"
            : "ticket.status_changed";

        await NotifyUserAsync(
            ticket.RequesterUserId,
            type,
            NotificationSeverity.Info,
            $"{ticket.TicketNumber} is now {ticket.Status}",
            $"Status changed from {previousStatus} to {ticket.Status}.",
            ticket.Id,
            EmployeeActionUrl(ticket.Id),
            cancellationToken);
    }

    public async Task NotifyAssignedAsync(
        TicketDto ticket,
        Guid? previousAssignee,
        CancellationToken cancellationToken = default)
    {
        if (ticket.AssignedUserId is not Guid assignee
            || assignee == Guid.Empty
            || assignee == previousAssignee)
        {
            return;
        }

        await NotifyUserAsync(
            assignee,
            "ticket.assigned",
            NotificationSeverity.Info,
            $"Assigned {ticket.TicketNumber}",
            $"You were assigned ticket \"{ticket.Title}\".",
            ticket.Id,
            ItActionUrl(ticket.Id),
            cancellationToken);
    }

    public async Task NotifyEmployeeVisibleCommentAsync(
        TicketDto ticket,
        Guid authorUserId,
        string bodyPreview,
        CancellationToken cancellationToken = default)
    {
        if (authorUserId == ticket.RequesterUserId)
        {
            if (ticket.AssignedUserId is Guid assignee && assignee != Guid.Empty)
            {
                await NotifyUserAsync(
                    assignee,
                    "ticket.employee_comment",
                    NotificationSeverity.Info,
                    $"New comment on {ticket.TicketNumber}",
                    Truncate(bodyPreview),
                    ticket.Id,
                    ItActionUrl(ticket.Id),
                    cancellationToken);
            }

            return;
        }

        await NotifyUserAsync(
            ticket.RequesterUserId,
            "ticket.comment",
            NotificationSeverity.Info,
            $"Update on {ticket.TicketNumber}",
            Truncate(bodyPreview),
            ticket.Id,
            EmployeeActionUrl(ticket.Id),
            cancellationToken);
    }

    public async Task NotifySlaBreachesAsync(
        IReadOnlyList<SlaBreachEvent> breaches,
        CancellationToken cancellationToken = default)
    {
        foreach (SlaBreachEvent breach in breaches)
        {
            if (breach.AssignedUserId is not Guid assignee || assignee == Guid.Empty)
            {
                continue;
            }

            if (breach.ResponseNewlyBreached)
            {
                await NotifyUserAsync(
                    assignee,
                    "ticket.sla.response_breach",
                    NotificationSeverity.Warning,
                    $"Response SLA breached: {breach.TicketNumber}",
                    $"Response due date was missed for {breach.TicketNumber}.",
                    breach.TicketId,
                    ItActionUrl(breach.TicketId),
                    cancellationToken);
            }

            if (breach.ResolutionNewlyBreached)
            {
                await NotifyUserAsync(
                    assignee,
                    "ticket.sla.resolution_breach",
                    NotificationSeverity.Critical,
                    $"Resolution SLA breached: {breach.TicketNumber}",
                    $"Resolution due date was missed for {breach.TicketNumber}.",
                    breach.TicketId,
                    ItActionUrl(breach.TicketId),
                    cancellationToken);
            }
        }
    }

    private async Task NotifyUserAsync(
        Guid recipientUserId,
        string type,
        NotificationSeverity severity,
        string title,
        string message,
        Guid ticketId,
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
                ticketId,
                actionUrl,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create in-app notification {Type} for user {UserId}", type, recipientUserId);
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
                BodyText = $"{message}\n\nOpen: {actionUrl}",
            });
        }
        catch (Exception ex)
        {
            // Email must never fail the ticket operation.
            logger.LogWarning(ex, "Failed to enqueue ticket email for user {UserId}", recipientUserId);
        }
    }

    private static string EmployeeActionUrl(Guid ticketId) => $"/employee/requests/{ticketId}";

    private static string ItActionUrl(Guid ticketId) => $"/it/tickets/{ticketId}";

    private static string Truncate(string value) =>
        value.Length <= 240 ? value : value[..237] + "...";
}
