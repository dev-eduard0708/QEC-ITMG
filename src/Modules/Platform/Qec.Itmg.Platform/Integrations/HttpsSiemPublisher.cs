using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qec.Itmg.Contracts.Integrations;
using Qec.Itmg.Contracts.Secrets;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Platform.Integrations;

/// <summary>
/// Queued/retryable SIEM publisher. Failures are recorded and do not throw to callers by default.
/// </summary>
public sealed class HttpsSiemPublisher(
    IHttpClientFactory httpClientFactory,
    IOptions<IntegrationOptions> options,
    ISecretResolver secrets,
    PlatformDbContext db,
    ILogger<HttpsSiemPublisher> logger,
    IntegrationHealthState health) : ISiemPublisher
{
    public IntegrationReadiness GetReadiness() =>
        IntegrationReadinessHelper.FromOptions(
            IntegrationProvider.Siem,
            options.Value.Siem,
            lastSuccess: health.Get(IntegrationProvider.Siem)?.LastSuccessUtc,
            lastFailure: health.Get(IntegrationProvider.Siem)?.LastFailureUtc,
            lastError: health.Get(IntegrationProvider.Siem)?.LastError,
            processed: health.Get(IntegrationProvider.Siem)?.LastProcessed);

    public async Task<SiemPublishResult> PublishAsync(SiemOutboundEvent evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        IntegrationVendorOptions opts = options.Value.Siem;
        if (!opts.Enabled)
            return new(false, false, null, "SIEM integration disabled.");
        if (!opts.IsConfigured)
            return new(false, false, null, "SIEM integration not configured.");

        try
        {
            string? token = await secrets.ResolveAsync(opts.CredentialReference, cancellationToken);
            if (string.IsNullOrEmpty(token))
                return new(false, false, null, "SIEM CredentialReference unresolved.");

            // Strip forbidden attribute keys.
            Dictionary<string, string> safeAttrs = evt.Attributes
                .Where(kv => !IsForbiddenKey(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var body = new
            {
                evt.EventId,
                timestampUtc = evt.TimestampUtc,
                evt.EventType,
                severity = evt.Severity.ToString(),
                evt.SourceSystem,
                evt.ActorUpn,
                evt.AggregateType,
                evt.AggregateId,
                evt.CorrelationId,
                attributes = safeAttrs,
            };

            HttpClient client = httpClientFactory.CreateClient("integrations-siem");
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.Timeout = TimeSpan.FromSeconds(15);

            HttpResponseMessage response = await client.PostAsJsonAsync("/api/events", body, cancellationToken);
            string deliveryId = evt.EventId;
            if (!response.IsSuccessStatusCode)
            {
                health.RecordFailure(IntegrationProvider.Siem, DateTimeOffset.UtcNow, $"HTTP {(int)response.StatusCode}");
                await RecordDeliveryAsync(evt, "Failed", cancellationToken);
                logger.LogWarning("SIEM delivery failed for {EventId} with {Status}", evt.EventId, (int)response.StatusCode);
                return new(false, true, deliveryId, "SIEM delivery failed; queued for retry tracking.");
            }

            health.RecordSuccess(IntegrationProvider.Siem, DateTimeOffset.UtcNow, 1);
            await RecordDeliveryAsync(evt, "Delivered", cancellationToken);
            return new(true, false, deliveryId, "Delivered.");
        }
        catch (Exception ex)
        {
            health.RecordFailure(IntegrationProvider.Siem, DateTimeOffset.UtcNow, "delivery-error");
            logger.LogError(ex, "SIEM publish error for {EventId}", evt.EventId);
            await RecordDeliveryAsync(evt, "Failed", cancellationToken);
            return new(false, true, evt.EventId, "SIEM publish error recorded; caller not blocked.");
        }
    }

    private async Task RecordDeliveryAsync(SiemOutboundEvent evt, string result, CancellationToken ct)
    {
        // Reuse webhook receipt table for outbound delivery idempotency tracking (provider=siem-out).
        string key = evt.EventId;
        bool exists = await db.IntegrationWebhookReceipts.AnyAsync(x => x.Provider == "siem-out" && x.ExternalEventId == key, ct);
        if (exists) return;
        IntegrationWebhookReceipt receipt = IntegrationWebhookReceipt.Create("siem-out", key, DateTimeOffset.UtcNow, evt.EventId);
        receipt.MarkProcessed(result, DateTimeOffset.UtcNow);
        db.IntegrationWebhookReceipts.Add(receipt);
        await db.SaveChangesAsync(ct);
    }

    private static bool IsForbiddenKey(string key)
    {
        string k = key.ToLowerInvariant();
        return k.Contains("password") || k.Contains("secret") || k.Contains("token")
            || k.Contains("credential") || k.Contains("authorization");
    }
}
