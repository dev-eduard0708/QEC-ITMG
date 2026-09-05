using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Qec.Itmg.Contracts.Integrations;
using Qec.Itmg.Contracts.Security;

namespace Qec.Itmg.Platform.Integrations;

public sealed record IntegrationReadinessDto(
    string Provider,
    bool Enabled,
    bool Configured,
    string RuntimeMode,
    string Status,
    bool ApprovalRequired,
    DateTimeOffset? LastSuccessfulSyncUtc,
    DateTimeOffset? LastFailureUtc,
    string? LastErrorSummary,
    int? LastProcessedCount,
    int? LastUnmatchedCount)
{
    public static IntegrationReadinessDto FromDomain(IntegrationReadiness r) =>
        new(
            r.Provider.ToString(),
            r.Enabled,
            r.Configured,
            r.RuntimeMode.ToString(),
            r.Status,
            r.ApprovalRequired,
            r.LastSuccessfulSyncUtc,
            r.LastFailureUtc,
            r.LastErrorSummary,
            r.LastProcessedCount,
            r.LastUnmatchedCount);
}

public static class IntegrationReadinessEndpoints
{
    public const string IntegrationsPermission = "admin.integrations";

    public static IEndpointRouteBuilder MapIntegrationReadinessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/admin/integrations/readiness", GetReadiness)
            .RequireAuthorization(IntegrationsPermission);

        endpoints.MapGet("/api/v1/admin/integrations/runs", async (
                IntegrationRunService runs,
                string? provider,
                int? take,
                CancellationToken ct) =>
            Results.Ok(await runs.ListAsync(provider, take ?? 30, ct)))
            .RequireAuthorization(IntegrationsPermission);

        endpoints.MapPost("/api/v1/admin/integrations/{provider}/sync", async (
                string provider,
                IntegrationSyncCoordinator sync,
                CancellationToken ct) => Results.Ok(await sync.SyncProviderAsync(provider, ct)))
            .RequireAuthorization(IntegrationsPermission);

        endpoints.MapPost("/api/v1/integrations/webhooks/{provider}", async (
                string provider,
                HttpRequest request,
                IIntegrationWebhookProcessor processor,
                IOptions<IntegrationOptions> integrationOptions,
                CancellationToken ct) =>
            {
                IntegrationVendorOptions opts = integrationOptions.Value.Webhook;
                if (request.ContentLength is long len && len > opts.MaxPayloadBytes)
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

                using MemoryStream ms = new();
                await request.Body.CopyToAsync(ms, ct);
                if (ms.Length > opts.MaxPayloadBytes)
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

                WebhookProcessResult result = await processor.ProcessAsync(
                    provider,
                    request.Headers["X-ITMG-Signature"].FirstOrDefault()
                        ?? request.Headers["X-Signature"].FirstOrDefault(),
                    request.Headers["X-ITMG-Timestamp"].FirstOrDefault()
                        ?? request.Headers["X-Timestamp"].FirstOrDefault(),
                    request.Headers["Idempotency-Key"].FirstOrDefault()
                        ?? request.Headers["X-Idempotency-Key"].FirstOrDefault(),
                    request.ContentType ?? string.Empty,
                    ms.ToArray(),
                    ct);

                return result.Accepted
                    ? Results.Ok(result)
                    : Results.Json(result, statusCode: result.Result is "forbidden" or "invalid_signature"
                        ? StatusCodes.Status401Unauthorized
                        : StatusCodes.Status400BadRequest);
            });

        return endpoints;
    }

    private static IResult GetReadiness(
        IVeeamClient veeam,
        ISonicWallCaptureClient sonicWall,
        ISynologyMonitor synology,
        IDirectorySyncClient directory,
        IVirtualizationEnrichmentClient virtualization,
        IVulnerabilityScannerIngestClient scanner,
        ISiemPublisher siem,
        MailIntegrationReadiness mail,
        IOptions<IntegrationOptions> options,
        IntegrationHealthState health)
    {
        IntegrationReadinessDto scannerReady = scanner is VulnerabilityScannerHttpClient vuln
            ? IntegrationReadinessDto.FromDomain(vuln.GetReadiness())
            : IntegrationReadinessDto.FromDomain(IntegrationReadinessHelper.FromOptions(
                IntegrationProvider.VulnerabilityScanner, options.Value.VulnerabilityScanner));

        IntegrationReadinessDto[] response =
        [
            IntegrationReadinessDto.FromDomain(veeam.GetReadiness()),
            IntegrationReadinessDto.FromDomain(sonicWall.GetReadiness()),
            IntegrationReadinessDto.FromDomain(synology.GetReadiness()),
            IntegrationReadinessDto.FromDomain(directory.GetReadiness()),
            IntegrationReadinessDto.FromDomain(mail.GetReadiness()),
            IntegrationReadinessDto.FromDomain(virtualization.GetReadiness()),
            scannerReady,
            IntegrationReadinessDto.FromDomain(siem.GetReadiness()),
            IntegrationReadinessDto.FromDomain(IntegrationReadinessHelper.FromOptions(
                IntegrationProvider.Webhook,
                options.Value.Webhook,
                requireBaseUrl: false,
                lastSuccess: health.Get(IntegrationProvider.Webhook)?.LastSuccessUtc,
                lastFailure: health.Get(IntegrationProvider.Webhook)?.LastFailureUtc,
                lastError: health.Get(IntegrationProvider.Webhook)?.LastError)),
        ];
        return Results.Ok(response);
    }
}
