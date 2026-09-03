using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Organization.Admin;

namespace Qec.Itmg.Host.Lookups;

public static class LookupAdminEndpoints
{
    public const string LookupsPermission = "admin.lookups";

    public static IEndpointRouteBuilder MapLookupAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/admin/lookups")
            .RequirePermission(LookupsPermission);

        group.MapGet("/departments", async (LookupAdminService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListDepartmentsAsync(cancellationToken)));

        group.MapPost("/departments", async (
            CreateLookupItemRequest request,
            LookupAdminService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateDepartmentAsync(request, cancellationToken)));

        group.MapPut("/departments/{id:guid}", async (
            Guid id,
            UpdateLookupItemRequest request,
            LookupAdminService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.UpdateDepartmentAsync(id, request, cancellationToken)));

        group.MapGet("/locations", async (LookupAdminService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListLocationsAsync(cancellationToken)));

        group.MapPost("/locations", async (
            CreateLookupItemRequest request,
            LookupAdminService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateLocationAsync(request, cancellationToken)));

        group.MapPut("/locations/{id:guid}", async (
            Guid id,
            UpdateLookupItemRequest request,
            LookupAdminService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.UpdateLocationAsync(id, request, cancellationToken)));

        return endpoints;
    }

    private static IResult ToHttpResult(LookupOperationResult result)
    {
        if (result.Item is not null && result.StatusCode is >= 200 and < 300)
        {
            return Results.Json(result.Item, statusCode: result.StatusCode);
        }

        return Results.Json(
            new
            {
                error = new
                {
                    code = result.ErrorCode,
                    message = result.ErrorMessage,
                    details = result.Field is null
                        ? Array.Empty<object>()
                        : new object[]
                        {
                            new
                            {
                                field = result.Field,
                                code = result.ErrorCode,
                                message = result.ErrorMessage,
                            },
                        },
                },
            },
            statusCode: result.StatusCode);
    }
}
