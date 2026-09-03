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
/// Placeholder sender until notification features land. Config is still bound for Mailpit/local SMTP.
/// </summary>
public sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Task.CompletedTask;
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
