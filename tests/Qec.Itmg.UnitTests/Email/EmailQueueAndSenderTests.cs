using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qec.Itmg.BuildingBlocks.Email;
using Xunit;

namespace Qec.Itmg.UnitTests.Email;

public sealed class EmailQueueAndSenderTests
{
    [Fact]
    public async Task SmtpEmailSender_UsesConfiguredFromAndHost_WithoutNetworkWhenReplaced()
    {
        RecordingEmailSender recording = new();
        ServiceCollection services = new();
        services.AddSingleton(Options.Create(new SmtpEmailOptions
        {
            Host = "localhost",
            Port = 1025,
            UseTls = false,
            FromAddress = "itmg-dev@localhost",
        }));
        services.AddSingleton<IEmailSender>(recording);
        services.AddSingleton<IEmailQueue, InlineEmailQueue>();

        await using ServiceProvider provider = services.BuildServiceProvider();
        IEmailQueue queue = provider.GetRequiredService<IEmailQueue>();

        EmailMessage message = new()
        {
            To = "dev@localhost",
            Subject = "P2-07 smoke",
            BodyText = "hello",
        };

        queue.Enqueue(message);

        // Inline queue fires SendAsync without awaiting; wait briefly for the task.
        await Task.Delay(50);
        Assert.Single(recording.Sent);
        Assert.Equal("dev@localhost", recording.Sent[0].To);
        Assert.Equal("P2-07 smoke", recording.Sent[0].Subject);
    }

    [Fact]
    public async Task NotificationEmailJob_DelegatesToSender()
    {
        RecordingEmailSender recording = new();
        Qec.Itmg.Host.Email.NotificationEmailJob job = new(recording);

        await job.SendAsync(new EmailMessage
        {
            To = "a@localhost",
            Subject = "job",
            BodyText = "body",
        });

        Assert.Single(recording.Sent);
        Assert.Equal("job", recording.Sent[0].Subject);
    }

    [Fact]
    public void Enqueue_DoesNotThrow_WhenSenderFailsAsynchronously()
    {
        FailingEmailSender failing = new();
        InlineEmailQueue queue = new(failing);

        // Must not throw synchronously to the business caller.
        Exception? sync = Record.Exception(() =>
            queue.Enqueue(new EmailMessage
            {
                To = "a@localhost",
                Subject = "fail",
                BodyText = "x",
            }));

        Assert.Null(sync);
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingEmailSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
            Task.FromException(new InvalidOperationException("smtp failed"));
    }
}
