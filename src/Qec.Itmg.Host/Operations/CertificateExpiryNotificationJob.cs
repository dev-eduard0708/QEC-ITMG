using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Identity.Seed;
using Qec.Itmg.Operations.Services;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Notifications;

namespace Qec.Itmg.Host.Operations;

public sealed class CertificateExpiryNotificationJob(
    CertificateExpiryService expiry,
    INotificationService notifications,
    IdentityDbContext identityDb,
    ILogger<CertificateExpiryNotificationJob> logger)
{
    public const string ResourceType = "Certificate";

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CertificateExpiryCandidate> due = await expiry.FindDueNotificationsAsync(cancellationToken);
        if (due.Count == 0) return 0;

        List<Guid> fallbackRecipients = await (
            from ur in identityDb.UserRoles.AsNoTracking()
            join role in identityDb.Roles.AsNoTracking() on ur.RoleId equals role.Id
            join user in identityDb.Users.AsNoTracking() on ur.UserId equals user.Id
            where role.Name == IdentitySeedCatalog.PlatformAdministratorRoleName
                  && user.Status == UserStatus.Active
            select user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        int sent = 0;
        foreach (CertificateExpiryCandidate item in due)
        {
            HashSet<Guid> recipients = [];
            if (item.OwnerUserId is Guid owner) recipients.Add(owner);
            if (recipients.Count == 0)
            {
                foreach (Guid id in fallbackRecipients) recipients.Add(id);
            }

            string type = item.ThresholdDays == 0 ? "certificate.expired" : "certificate.expiring";
            NotificationSeverity severity = item.ThresholdDays switch
            {
                0 => NotificationSeverity.Critical,
                1 or 7 => NotificationSeverity.Warning,
                _ => NotificationSeverity.Info,
            };
            string title = item.ThresholdDays == 0
                ? $"Certificate expired: {item.Name}"
                : $"Certificate expiring in {item.DaysToExpiry} day(s): {item.Name}";
            string message = item.ThresholdDays == 0
                ? $"\"{item.Name}\" expired at {item.ExpiresAtUtc:u}."
                : $"\"{item.Name}\" expires at {item.ExpiresAtUtc:u} ({item.DaysToExpiry} day(s) remaining).";

            foreach (Guid recipientId in recipients)
            {
                await notifications.CreateAsync(
                    recipientId,
                    type,
                    severity,
                    title,
                    message,
                    ResourceType,
                    item.CertificateId,
                    "/it/operations?tab=certificates",
                    cancellationToken);
                sent++;
            }

            await expiry.MarkNotifiedAsync(item.CertificateId, item.ThresholdDays, cancellationToken);
        }

        logger.LogInformation("Certificate expiry job notified {CandidateCount} threshold(s), {Sent} notification(s).", due.Count, sent);
        return sent;
    }
}

public sealed class EventRetentionJob(EventRetentionService retention)
{
    public Task<int> ExecuteAsync(CancellationToken cancellationToken = default) =>
        retention.PurgeClosedEventsAsync(cancellationToken);
}
