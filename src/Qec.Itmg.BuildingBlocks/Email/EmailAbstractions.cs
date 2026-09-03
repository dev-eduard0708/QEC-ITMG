namespace Qec.Itmg.BuildingBlocks.Email;

public sealed class EmailMessage
{
    public required string To { get; init; }

    public required string Subject { get; init; }

    public string? BodyText { get; init; }

    public string? BodyHtml { get; init; }
}

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Enqueues email for asynchronous delivery. Failures must not roll back business transactions.
/// </summary>
public interface IEmailQueue
{
    void Enqueue(EmailMessage message);
}

/// <summary>
/// Placeholder sender for tests or disabled environments.
/// </summary>
public sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Task.CompletedTask;
    }
}

/// <summary>
/// SMTP sender for development (Mailpit) and future on-prem SMTP.
/// </summary>
public sealed class SmtpEmailSender(Microsoft.Extensions.Options.IOptions<SmtpEmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.To);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Subject);

        SmtpEmailOptions smtp = options.Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(smtp.Host);
        ArgumentException.ThrowIfNullOrWhiteSpace(smtp.FromAddress);

        using System.Net.Mail.MailMessage mail = new()
        {
            From = new System.Net.Mail.MailAddress(
                smtp.FromAddress,
                string.IsNullOrWhiteSpace(smtp.FromDisplayName) ? null : smtp.FromDisplayName),
            Subject = message.Subject,
            Body = message.BodyHtml ?? message.BodyText ?? string.Empty,
            IsBodyHtml = !string.IsNullOrWhiteSpace(message.BodyHtml),
        };
        mail.To.Add(message.To);

        using System.Net.Mail.SmtpClient client = new(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.UseTls,
            DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network,
        };

        if (!string.IsNullOrWhiteSpace(smtp.Username))
        {
            client.Credentials = new System.Net.NetworkCredential(smtp.Username, smtp.Password);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(mail);
    }
}

/// <summary>
/// Test/dev fallback that sends inline without Hangfire.
/// </summary>
public sealed class InlineEmailQueue(IEmailSender sender) : IEmailQueue
{
    public void Enqueue(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        // Fire-and-forget intentionally: callers must not await SMTP inside a business transaction.
        _ = sender.SendAsync(message);
    }
}

public sealed class SmtpEmailOptions
{
    public const string SectionName = "Email:Smtp";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public bool UseTls { get; set; }

    public string FromAddress { get; set; } = "itmg-dev@localhost";

    public string? FromDisplayName { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }
}
