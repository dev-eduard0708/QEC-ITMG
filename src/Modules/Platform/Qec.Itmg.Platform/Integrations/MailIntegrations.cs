using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qec.Itmg.BuildingBlocks.Email;
using Qec.Itmg.Contracts.Integrations;
using Qec.Itmg.Contracts.Secrets;

namespace Qec.Itmg.Platform.Integrations;

/// <summary>
/// M365 Graph mail sender. Used only when Integrations:Mail is enabled and configured.
/// SMTP/Mailpit remains the default via SmtpEmailSender.
/// </summary>
public sealed class GraphMailSender(
    IHttpClientFactory httpClientFactory,
    IOptions<IntegrationOptions> options,
    ISecretResolver secrets,
    ILogger<GraphMailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        IntegrationVendorOptions opts = options.Value.Mail;
        IntegrationReadinessHelper.EnsureCallable(opts, "Mail");

        string? token = await secrets.ResolveAsync(opts.CredentialReference, cancellationToken);
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("Mail CredentialReference could not be resolved.");

        string mailbox = string.IsNullOrWhiteSpace(opts.MailboxAddress)
            ? throw new InvalidOperationException("Integrations:Mail:MailboxAddress is required for Graph mail.")
            : opts.MailboxAddress.Trim();

        HttpClient client = httpClientFactory.CreateClient("integrations-mail");
        client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            message = new
            {
                subject = message.Subject,
                body = new
                {
                    contentType = string.IsNullOrWhiteSpace(message.BodyHtml) ? "Text" : "HTML",
                    content = message.BodyHtml ?? message.BodyText ?? string.Empty,
                },
                toRecipients = new[]
                {
                    new { emailAddress = new { address = message.To } },
                },
            },
            saveToSentItems = false,
        };

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/v1.0/users/{Uri.EscapeDataString(mailbox)}/sendMail",
            payload,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Graph mail send failed with status {Status}", (int)response.StatusCode);
            throw new InvalidOperationException($"Graph mail send failed ({(int)response.StatusCode}).");
        }
    }
}

/// <summary>
/// Selects SMTP (default) or Graph mail based on Integrations:Mail without silently enabling Graph.
/// </summary>
public sealed class ConfigurableEmailSender(
    IOptions<IntegrationOptions> options,
    SmtpEmailSender smtp,
    GraphMailSender graph) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        IntegrationVendorOptions mail = options.Value.Mail;
        if (mail.Enabled && mail.IsConfigured
            && mail.ProviderKind.Equals("Graph", StringComparison.OrdinalIgnoreCase))
        {
            return graph.SendAsync(message, cancellationToken);
        }

        return smtp.SendAsync(message, cancellationToken);
    }
}

public sealed class MailIntegrationReadiness(
    IOptions<IntegrationOptions> options,
    IntegrationHealthState health)
{
    public IntegrationReadiness GetReadiness() =>
        IntegrationReadinessHelper.FromOptions(
            IntegrationProvider.Mail,
            options.Value.Mail,
            lastSuccess: health.Get(IntegrationProvider.Mail)?.LastSuccessUtc,
            lastFailure: health.Get(IntegrationProvider.Mail)?.LastFailureUtc,
            lastError: health.Get(IntegrationProvider.Mail)?.LastError);
}
