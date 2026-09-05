using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Integrations;
using Qec.Itmg.Contracts.Secrets;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Platform.Integrations;

public sealed class IntegrationWebhookProcessor(
    IOptions<IntegrationOptions> options,
    ISecretResolver secrets,
    PlatformDbContext db,
    IClock clock,
    ILogger<IntegrationWebhookProcessor> logger) : IIntegrationWebhookProcessor
{
    public async Task<WebhookProcessResult> ProcessAsync(
        string provider,
        string? signatureHeader,
        string? timestampHeader,
        string? idempotencyKey,
        string contentType,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
    {
        IntegrationVendorOptions opts = options.Value.Webhook;
        string providerKey = (provider ?? string.Empty).Trim().ToLowerInvariant();

        HashSet<string> allow = options.Value.WebhookProviderAllowlist
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!opts.Enabled)
            return Reject(providerKey, "disabled", "Webhook integration disabled.");

        if (!allow.Contains(providerKey))
            return Reject(providerKey, "forbidden", "Provider not allowlisted.");

        if (!string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase)
            && !contentType.StartsWith("application/json;", StringComparison.OrdinalIgnoreCase))
            return Reject(providerKey, "unsupported_media", "Content-Type must be application/json.");

        int maxBytes = opts.MaxPayloadBytes <= 0 ? 256 * 1024 : opts.MaxPayloadBytes;
        if (body.Length > maxBytes)
            return Reject(providerKey, "payload_too_large", "Payload exceeds configured limit.");

        if (string.IsNullOrWhiteSpace(opts.WebhookSignatureReference) && string.IsNullOrWhiteSpace(opts.CredentialReference))
            return Reject(providerKey, "not_configured", "Webhook signature CredentialReference missing.");

        string secretRef = string.IsNullOrWhiteSpace(opts.WebhookSignatureReference)
            ? opts.CredentialReference
            : opts.WebhookSignatureReference!;
        string? secret = await secrets.ResolveAsync(secretRef, cancellationToken);
        if (string.IsNullOrEmpty(secret))
            return Reject(providerKey, "not_configured", "Webhook signature secret unresolved.");

        if (!ValidateTimestamp(timestampHeader, opts.TimestampSkewSeconds))
            return Reject(providerKey, "stale", "Timestamp missing or outside freshness window.");

        if (!ValidateHmac(signatureHeader, timestampHeader, body.Span, secret))
        {
            logger.LogWarning("Webhook HMAC validation failed for provider {Provider}", providerKey);
            return Reject(providerKey, "invalid_signature", "Signature validation failed.");
        }

        string externalEventId = !string.IsNullOrWhiteSpace(idempotencyKey)
            ? idempotencyKey.Trim()
            : ComputeHash(body.Span);
        string payloadHash = ComputeHash(body.Span);
        DateTimeOffset now = clock.UtcNow;

        IntegrationWebhookReceipt? existing = await db.IntegrationWebhookReceipts
            .FirstOrDefaultAsync(x => x.Provider == providerKey && x.ExternalEventId == externalEventId, cancellationToken);
        if (existing is not null)
        {
            return new WebhookProcessResult(true, true, providerKey, externalEventId, existing.Result, "Duplicate suppressed.");
        }

        IntegrationWebhookReceipt receipt = IntegrationWebhookReceipt.Create(providerKey, externalEventId, now, payloadHash);
        try
        {
            // Intentionally do not persist raw payload. Provider-specific handlers can be added later.
            receipt.MarkProcessed("Accepted", clock.UtcNow);
            db.IntegrationWebhookReceipts.Add(receipt);
            await db.SaveChangesAsync(cancellationToken);
            return new WebhookProcessResult(true, false, providerKey, externalEventId, "Accepted", null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Webhook processing failed for {Provider}", providerKey);
            receipt.MarkProcessed("Failed", clock.UtcNow, "processing-error");
            db.IntegrationWebhookReceipts.Add(receipt);
            await db.SaveChangesAsync(cancellationToken);
            return new WebhookProcessResult(false, false, providerKey, externalEventId, "Failed", "Processing error.");
        }
    }

    private static bool ValidateTimestamp(string? timestampHeader, int skewSeconds)
    {
        if (string.IsNullOrWhiteSpace(timestampHeader))
            return false;
        if (!long.TryParse(timestampHeader, out long unix))
        {
            if (!DateTimeOffset.TryParse(timestampHeader, out DateTimeOffset dto))
                return false;
            unix = dto.ToUnixTimeSeconds();
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return Math.Abs(now - unix) <= Math.Max(30, skewSeconds);
    }

    private static bool ValidateHmac(string? signatureHeader, string? timestampHeader, ReadOnlySpan<byte> body, string secret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        string provided = signatureHeader.Trim();
        if (provided.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            provided = provided["sha256=".Length..];

        byte[] key = Encoding.UTF8.GetBytes(secret);
        string payload = $"{timestampHeader ?? "."}.{Convert.ToBase64String(body)}";
        byte[] expectedBytes = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload));
        string expected = Convert.ToHexString(expectedBytes).ToLowerInvariant();
        string providedNorm = provided.ToLowerInvariant();
        if (expected.Length != providedNorm.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(providedNorm));
    }

    private static string ComputeHash(ReadOnlySpan<byte> body) =>
        Convert.ToHexString(SHA256.HashData(body));

    private static WebhookProcessResult Reject(string provider, string result, string message) =>
        new(false, false, provider, string.Empty, result, message);
}
