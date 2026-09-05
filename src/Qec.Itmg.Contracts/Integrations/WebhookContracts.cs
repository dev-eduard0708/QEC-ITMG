namespace Qec.Itmg.Contracts.Integrations;

public sealed record WebhookProcessResult(
    bool Accepted,
    bool Duplicate,
    string Provider,
    string ExternalEventId,
    string Result,
    string? Message);

public interface IIntegrationWebhookProcessor
{
    Task<WebhookProcessResult> ProcessAsync(
        string provider,
        string? signatureHeader,
        string? timestampHeader,
        string? idempotencyKey,
        string contentType,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default);
}
