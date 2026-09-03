using Hangfire;
using Qec.Itmg.BuildingBlocks.Email;

namespace Qec.Itmg.Host.Email;

/// <summary>
/// Hangfire-backed email job. Retries are handled by Hangfire.
/// </summary>
public sealed class NotificationEmailJob(IEmailSender emailSender)
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return emailSender.SendAsync(message, cancellationToken);
    }
}

public sealed class HangfireEmailQueue : IEmailQueue
{
    public void Enqueue(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        BackgroundJob.Enqueue<NotificationEmailJob>(job => job.SendAsync(message, CancellationToken.None));
    }
}
