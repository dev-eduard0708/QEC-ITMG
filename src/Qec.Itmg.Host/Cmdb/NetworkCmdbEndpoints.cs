using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;

namespace Qec.Itmg.Host.Cmdb;

public static class NetworkCmdbEndpoints
{
    public const string CmdbRead = CmdbEndpoints.CmdbRead;
    public const string CmdbManage = CmdbEndpoints.CmdbManage;
    public const string CmdbRelationshipManage = "cmdb.relationship.manage";
    public const string CmdbDiscoveryManage = "cmdb.discovery.manage";

    public static IEndpointRouteBuilder MapNetworkCmdbEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder readGroup = endpoints.MapGroup("/api/v1/cmdb").RequirePermission(CmdbRead);

        readGroup.MapGet("/network-topology/views", async (
            NetworkTopologyService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListViewsAsync(cancellationToken)));

        readGroup.MapGet("/network-topology/graph", async (
            Guid? viewId,
            Guid? locationId,
            string? type,
            string? search,
            NetworkTopologyService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListTopologyGraphAsync(viewId, locationId, type, search, cancellationToken)));

        readGroup.MapGet("/cis/{id:guid}/network-identities", async (
            Guid id,
            NetworkTopologyService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListIdentitiesAsync(id, cancellationToken)));

        RouteGroupBuilder manageGroup = endpoints.MapGroup("/api/v1/cmdb").RequirePermission(CmdbManage);

        manageGroup.MapPost("/network-topology/views", async (
            CreateTopologyViewRequest request,
            NetworkTopologyService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return CmdbEndpoints.ValidationProblem("name is required.");
            }

            try
            {
                NetworkTopologyViewDto created = await service.CreateViewAsync(
                    request.Name,
                    request.LocationId,
                    request.Description,
                    request.IsDefault,
                    cancellationToken);
                return Results.Created($"/api/v1/cmdb/network-topology/views/{created.Id}", created);
            }
            catch (ArgumentException ex)
            {
                return CmdbEndpoints.ValidationProblem(ex.Message);
            }
        });

        manageGroup.MapPut("/network-topology/views/{id:guid}/layouts", async (
            Guid id,
            SaveNodeLayoutsRequest request,
            NetworkTopologyService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                IReadOnlyList<NodeLayoutItem> layouts = (request.Layouts ?? [])
                    .Select(item => new NodeLayoutItem(item.ConfigurationItemId, item.PositionX, item.PositionY))
                    .ToList();
                await service.SaveNodeLayoutsAsync(id, layouts, cancellationToken);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return CmdbEndpoints.FromDomainError(ex);
            }
        });

        RouteGroupBuilder relationshipGroup = endpoints.MapGroup("/api/v1/cmdb")
            .RequirePermission(CmdbRelationshipManage);

        relationshipGroup.MapPost("/network-topology/connections", async (
            CreateNetworkConnectionRequest request,
            NetworkTopologyService service,
            CancellationToken cancellationToken) =>
        {
            if (request.SourceCiId == Guid.Empty || request.TargetCiId == Guid.Empty)
            {
                return CmdbEndpoints.ValidationProblem("sourceCiId and targetCiId are required.");
            }

            try
            {
                NetworkLinkDetailDto created = await service.ConnectAsync(
                    request.SourceCiId,
                    request.TargetCiId,
                    request.FromPort,
                    request.ToPort,
                    request.MediaType,
                    request.LinkMode,
                    request.Notes,
                    cancellationToken);
                return Results.Created(
                    $"/api/v1/cmdb/network-topology/link-details/{created.Id}",
                    created);
            }
            catch (InvalidOperationException ex)
            {
                return CmdbEndpoints.FromDomainError(ex);
            }
            catch (ArgumentException ex)
            {
                return CmdbEndpoints.ValidationProblem(ex.Message);
            }
        });

        relationshipGroup.MapPut("/network-topology/relationships/{id:guid}/link-detail", async (
            Guid id,
            UpdateNetworkLinkDetailRequest request,
            NetworkTopologyService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                NetworkLinkDetailDto? updated = await service.UpdateLinkDetailAsync(
                    id,
                    request.FromPort,
                    request.ToPort,
                    request.MediaType,
                    request.LinkMode,
                    request.Notes,
                    request.IsConfirmed,
                    cancellationToken);
                return updated is null ? Results.NotFound() : Results.Ok(updated);
            }
            catch (ArgumentException ex)
            {
                return CmdbEndpoints.ValidationProblem(ex.Message);
            }
        });

        relationshipGroup.MapDelete("/network-topology/relationships/{id:guid}", async (
            Guid id,
            NetworkTopologyService service,
            CancellationToken cancellationToken) =>
        {
            bool deleted = await service.DeleteConnectionAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        RouteGroupBuilder discoveryRead = endpoints.MapGroup("/api/v1/cmdb")
            .RequirePermission(CmdbRead);

        discoveryRead.MapGet("/network-discovery/profiles", async (
            NetworkDiscoveryService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListProfilesAsync(cancellationToken)));

        discoveryRead.MapGet("/network-discovery/profiles/{id:guid}", async (
            Guid id,
            NetworkDiscoveryService service,
            CancellationToken cancellationToken) =>
        {
            NetworkDiscoveryProfileDto? item = await service.GetProfileAsync(id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        discoveryRead.MapGet("/network-discovery/runs/{id:guid}", async (
            Guid id,
            NetworkDiscoveryService service,
            CancellationToken cancellationToken) =>
        {
            NetworkDiscoveryRunDto? item = await service.GetRunAsync(id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        discoveryRead.MapGet("/network-discovery/runs/{id:guid}/observations", async (
            Guid id,
            NetworkDiscoveryService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListObservationsAsync(id, cancellationToken)));

        RouteGroupBuilder discoveryManage = endpoints.MapGroup("/api/v1/cmdb")
            .RequirePermission(CmdbDiscoveryManage);

        discoveryManage.MapPost("/network-discovery/profiles", async (
            CreateDiscoveryProfileRequest request,
            NetworkDiscoveryService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Cidr))
            {
                return CmdbEndpoints.ValidationProblem("name and cidr are required.");
            }

            try
            {
                NetworkDiscoveryProfileDto created = await service.CreateProfileAsync(
                    request.Name,
                    request.Cidr,
                    request.LocationId,
                    request.TimeoutMs,
                    request.MaxConcurrency,
                    request.IsActive ?? true,
                    cancellationToken);
                return Results.Created($"/api/v1/cmdb/network-discovery/profiles/{created.Id}", created);
            }
            catch (InvalidOperationException ex)
            {
                return CmdbEndpoints.FromDomainError(ex);
            }
            catch (ArgumentException ex)
            {
                return CmdbEndpoints.ValidationProblem(ex.Message);
            }
        });

        discoveryManage.MapPut("/network-discovery/profiles/{id:guid}", async (
            Guid id,
            UpdateDiscoveryProfileRequest request,
            NetworkDiscoveryService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Cidr))
            {
                return CmdbEndpoints.ValidationProblem("name and cidr are required.");
            }

            try
            {
                return Results.Ok(await service.UpdateProfileAsync(
                    id,
                    request.Name,
                    request.Cidr,
                    request.LocationId,
                    request.IsActive,
                    request.TimeoutMs,
                    request.MaxConcurrency,
                    cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return CmdbEndpoints.FromDomainError(ex);
            }
            catch (ArgumentException ex)
            {
                return CmdbEndpoints.ValidationProblem(ex.Message);
            }
        });

        discoveryManage.MapDelete("/network-discovery/profiles/{id:guid}", async (
            Guid id,
            NetworkDiscoveryService service,
            CancellationToken cancellationToken) =>
        {
            bool deleted = await service.DeleteProfileAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        discoveryManage.MapPost("/network-discovery/profiles/{id:guid}/scan", async (
            Guid id,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            NetworkDiscoveryService service,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return Results.Json(
                    new { error = new { code = "session_unavailable", message = "No active ITMG user session." } },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            try
            {
                NetworkDiscoveryRunDto run = await service.StartRunAsync(id, session.Id, cancellationToken);
                return Results.Accepted($"/api/v1/cmdb/network-discovery/runs/{run.Id}", run);
            }
            catch (InvalidOperationException ex)
            {
                return CmdbEndpoints.FromDomainError(ex);
            }
            catch (ArgumentException ex)
            {
                return CmdbEndpoints.ValidationProblem(ex.Message);
            }
        });

        discoveryManage.MapPost("/network-discovery/observations/{id:guid}/match", async (
            Guid id,
            MatchObservationRequest request,
            NetworkDiscoveryService service,
            CancellationToken cancellationToken) =>
        {
            if (request.ConfigurationItemId == Guid.Empty)
            {
                return CmdbEndpoints.ValidationProblem("configurationItemId is required.");
            }

            try
            {
                return Results.Ok(await service.MatchExistingAsync(
                    id,
                    request.ConfigurationItemId,
                    request.AddIdentityIfMissing ?? true,
                    cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return CmdbEndpoints.FromDomainError(ex);
            }
        });

        discoveryManage.MapPost("/network-discovery/observations/{id:guid}/create-ci", async (
            Guid id,
            CreateCiFromObservationRequest request,
            NetworkDiscoveryService service,
            CancellationToken cancellationToken) =>
        {
            if (request.CiTypeId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name))
            {
                return CmdbEndpoints.ValidationProblem("ciTypeId and name are required.");
            }

            try
            {
                return Results.Ok(await service.CreateCiFromObservationAsync(
                    id,
                    request.CiTypeId,
                    request.Name,
                    request.LocationId,
                    request.Description,
                    cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return CmdbEndpoints.FromDomainError(ex);
            }
            catch (ArgumentException ex)
            {
                return CmdbEndpoints.ValidationProblem(ex.Message);
            }
        });

        discoveryManage.MapPost("/network-discovery/observations/{id:guid}/accept-changes", async (
            Guid id,
            AcceptObservationChangesRequest? request,
            NetworkDiscoveryService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await service.AcceptChangesAsync(
                    id,
                    request?.UpdateIp ?? true,
                    request?.UpdateHostname ?? true,
                    request?.UpdateMac ?? true,
                    cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return CmdbEndpoints.FromDomainError(ex);
            }
        });

        discoveryManage.MapPost("/network-discovery/observations/{id:guid}/ignore", async (
            Guid id,
            NetworkDiscoveryService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await service.IgnoreAsync(id, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return CmdbEndpoints.FromDomainError(ex);
            }
        });

        return endpoints;
    }
}

public sealed record CreateTopologyViewRequest(
    string Name,
    Guid? LocationId,
    string? Description,
    bool IsDefault);

public sealed record SaveNodeLayoutsRequest(IReadOnlyList<SaveNodeLayoutItem>? Layouts);

public sealed record SaveNodeLayoutItem(Guid ConfigurationItemId, double PositionX, double PositionY);

public sealed record CreateNetworkConnectionRequest(
    Guid SourceCiId,
    Guid TargetCiId,
    string? FromPort,
    string? ToPort,
    string? MediaType,
    string? LinkMode,
    string? Notes);

public sealed record UpdateNetworkLinkDetailRequest(
    string? FromPort,
    string? ToPort,
    string? MediaType,
    string? LinkMode,
    string? Notes,
    bool IsConfirmed);

public sealed record CreateDiscoveryProfileRequest(
    string Name,
    string Cidr,
    Guid? LocationId,
    int? TimeoutMs,
    int? MaxConcurrency,
    bool? IsActive);

public sealed record UpdateDiscoveryProfileRequest(
    string Name,
    string Cidr,
    Guid? LocationId,
    bool IsActive,
    int TimeoutMs,
    int MaxConcurrency);

public sealed record MatchObservationRequest(Guid ConfigurationItemId, bool? AddIdentityIfMissing);

public sealed record CreateCiFromObservationRequest(
    Guid CiTypeId,
    string Name,
    Guid? LocationId,
    string? Description);

public sealed record AcceptObservationChangesRequest(bool? UpdateIp, bool? UpdateHostname, bool? UpdateMac);
