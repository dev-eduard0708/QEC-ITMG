using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Contracts.Integrations;

namespace Qec.Itmg.Platform.Integrations;

public sealed record IntegrationReadinessDto(
    string Provider,
    bool Enabled,
    bool Configured,
    string RuntimeMode,
    bool ApprovalRequired)
{
    public static IntegrationReadinessDto FromDomain(IntegrationReadiness r) =>
        new(
            r.Provider.ToString(),
            r.Enabled,
            r.Configured,
            r.RuntimeMode.ToString(),
            r.ApprovalRequired);
}

public static class IntegrationReadinessEndpoints
{
    public const string IntegrationsPermission = "admin.integrations";

    public static IEndpointRouteBuilder MapIntegrationReadinessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/admin/integrations/readiness", (
                IVeeamClient veeam,
                ISonicWallCaptureClient sonicWall,
                ISynologyMonitor synology) =>
            {
                IntegrationReadinessDto[] response =
                [
                    IntegrationReadinessDto.FromDomain(veeam.GetReadiness()),
                    IntegrationReadinessDto.FromDomain(sonicWall.GetReadiness()),
                    IntegrationReadinessDto.FromDomain(synology.GetReadiness()),
                ];
                return Results.Ok(response);
            })
            .RequireAuthorization(IntegrationsPermission);

        return endpoints;
    }
}
