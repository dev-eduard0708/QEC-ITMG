using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Cmdb.Domain;
using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;

namespace Qec.Itmg.Host.Cmdb;

public static class CmdbEndpoints
{
    public const string CmdbRead = "cmdb.read";
    public const string CmdbManage = "cmdb.manage";

    public static IEndpointRouteBuilder MapCmdbEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder readGroup = endpoints.MapGroup("/api/v1/cmdb")
            .RequirePermission(CmdbRead);

        readGroup.MapGet("/ci-types", async (ConfigurationItemService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListCiTypesAsync(cancellationToken)));

        readGroup.MapGet("/cis", async (
            string? search,
            ConfigurationItemService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListConfigurationItemsAsync(search, cancellationToken)));

        readGroup.MapGet("/cis/{id:guid}", async (
            Guid id,
            ConfigurationItemService service,
            CancellationToken cancellationToken) =>
        {
            ConfigurationItemDto? item = await service.GetConfigurationItemAsync(id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        readGroup.MapGet("/cis/{id:guid}/relationships", async (
            Guid id,
            ConfigurationItemService cis,
            CiRelationshipService relationships,
            CancellationToken cancellationToken) =>
        {
            if (await cis.GetConfigurationItemAsync(id, cancellationToken) is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(await relationships.ListForCiAsync(id, cancellationToken));
        });

        readGroup.MapGet("/services", async (BusinessServiceService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken)));

        readGroup.MapGet("/services/{id:guid}", async (
            Guid id,
            BusinessServiceService service,
            CancellationToken cancellationToken) =>
        {
            BusinessServiceDto? item = await service.GetAsync(id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        RouteGroupBuilder manageGroup = endpoints.MapGroup("/api/v1/cmdb")
            .RequirePermission(CmdbManage);

        manageGroup.MapPost("/cis", async (
            CreateConfigurationItemRequest request,
            ConfigurationItemService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.CiTypeId == Guid.Empty)
            {
                return ValidationProblem("name and ciTypeId are required.");
            }

            try
            {
                ConfigurationItem created = await service.CreateConfigurationItemAsync(
                    request.CiTypeId,
                    request.Name,
                    request.Description,
                    ParseOptionalEnum<CiCriticality>(request.Criticality),
                    request.LocationId,
                    request.DepartmentId,
                    request.OwnerUserId,
                    request.SerialNumber,
                    request.Manufacturer,
                    request.Model,
                    request.Notes,
                    cancellationToken);
                ConfigurationItemDto? dto = await service.GetConfigurationItemAsync(created.Id, cancellationToken);
                return Results.Created($"/api/v1/cmdb/cis/{created.Id}", dto);
            }
            catch (InvalidOperationException ex)
            {
                return FromDomainError(ex);
            }
            catch (ArgumentException ex)
            {
                return ValidationProblem(ex.Message);
            }
        });

        manageGroup.MapPut("/cis/{id:guid}", async (
            Guid id,
            UpdateConfigurationItemRequest request,
            ConfigurationItemService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Status))
            {
                return ValidationProblem("name and status are required.");
            }

            if (!Enum.TryParse(request.Status, ignoreCase: true, out ConfigurationItemStatus status))
            {
                return ValidationProblem("Invalid status.");
            }

            try
            {
                await service.UpdateConfigurationItemAsync(
                    id,
                    request.Name,
                    request.Description,
                    status,
                    ParseOptionalEnum<CiCriticality>(request.Criticality),
                    request.LocationId,
                    request.DepartmentId,
                    request.OwnerUserId,
                    request.SerialNumber,
                    request.Manufacturer,
                    request.Model,
                    request.Notes,
                    request.RowVersion ?? string.Empty,
                    cancellationToken);
                return Results.Ok(await service.GetConfigurationItemAsync(id, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return FromDomainError(ex);
            }
            catch (ArgumentException ex)
            {
                return ValidationProblem(ex.Message);
            }
        });

        manageGroup.MapPost("/cis/{id:guid}/relationships", async (
            Guid id,
            CreateCiRelationshipRequest request,
            CiRelationshipService service,
            CancellationToken cancellationToken) =>
        {
            if (request.TargetCiId == Guid.Empty
                || string.IsNullOrWhiteSpace(request.RelationshipType)
                || !Enum.TryParse(request.RelationshipType, ignoreCase: true, out CiRelationshipType type))
            {
                return ValidationProblem("targetCiId and a valid relationshipType are required.");
            }

            try
            {
                CiRelationship created = await service.CreateAsync(
                    id,
                    request.TargetCiId,
                    type,
                    request.Notes,
                    cancellationToken);
                IReadOnlyList<CiRelationshipDto> list = await service.ListForCiAsync(id, cancellationToken);
                CiRelationshipDto dto = list.First(item => item.Id == created.Id);
                return Results.Created($"/api/v1/cmdb/relationships/{created.Id}", dto);
            }
            catch (InvalidOperationException ex)
            {
                return FromDomainError(ex);
            }
            catch (ArgumentException ex)
            {
                return ValidationProblem(ex.Message);
            }
        });

        manageGroup.MapDelete("/relationships/{id:guid}", async (
            Guid id,
            CiRelationshipService service,
            CancellationToken cancellationToken) =>
        {
            bool deleted = await service.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        manageGroup.MapPost("/services", async (
            CreateBusinessServiceRequest request,
            BusinessServiceService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)
                || string.IsNullOrWhiteSpace(request.Criticality)
                || !Enum.TryParse(request.Criticality, ignoreCase: true, out CiCriticality criticality))
            {
                return ValidationProblem("name and a valid criticality are required.");
            }

            try
            {
                BusinessService created = await service.CreateAsync(
                    request.Name,
                    criticality,
                    request.Description,
                    request.OwnerUserId,
                    request.RtoMinutes,
                    request.RpoMinutes,
                    cancellationToken);
                return Results.Created($"/api/v1/cmdb/services/{created.Id}", await service.GetAsync(created.Id, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return ValidationProblem(ex.Message);
            }
        });

        manageGroup.MapPut("/services/{id:guid}", async (
            Guid id,
            UpdateBusinessServiceRequest request,
            BusinessServiceService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)
                || string.IsNullOrWhiteSpace(request.Criticality)
                || !Enum.TryParse(request.Criticality, ignoreCase: true, out CiCriticality criticality))
            {
                return ValidationProblem("name and a valid criticality are required.");
            }

            try
            {
                await service.UpdateAsync(
                    id,
                    request.Name,
                    criticality,
                    request.Description,
                    request.OwnerUserId,
                    request.RtoMinutes,
                    request.RpoMinutes,
                    request.IsActive,
                    cancellationToken);
                return Results.Ok(await service.GetAsync(id, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return FromDomainError(ex);
            }
            catch (ArgumentException ex)
            {
                return ValidationProblem(ex.Message);
            }
        });

        return endpoints;
    }

    internal static IResult ValidationProblem(string message) =>
        Results.Json(
            new { error = new { code = "validation_error", message } },
            statusCode: StatusCodes.Status400BadRequest);

    internal static IResult FromDomainError(InvalidOperationException ex)
    {
        string message = ex.Message;
        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(
                new { error = new { code = "not_found", message } },
                statusCode: StatusCodes.Status404NotFound);
        }

        if (message.Contains("already", StringComparison.OrdinalIgnoreCase)
            || message.Contains("modified by another", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(
                new { error = new { code = "conflict", message } },
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Json(
            new { error = new { code = "invalid_operation", message } },
            statusCode: StatusCodes.Status400BadRequest);
    }

    private static TEnum? ParseOptionalEnum<TEnum>(string? value)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse(value, ignoreCase: true, out TEnum parsed) ? parsed : null;
    }
}

public sealed record CreateConfigurationItemRequest(
    Guid CiTypeId,
    string Name,
    string? Description,
    string? Criticality,
    Guid? LocationId,
    Guid? DepartmentId,
    Guid? OwnerUserId,
    string? SerialNumber,
    string? Manufacturer,
    string? Model,
    string? Notes);

public sealed record UpdateConfigurationItemRequest(
    string Name,
    string? Description,
    string Status,
    string? Criticality,
    Guid? LocationId,
    Guid? DepartmentId,
    Guid? OwnerUserId,
    string? SerialNumber,
    string? Manufacturer,
    string? Model,
    string? Notes,
    string? RowVersion);

public sealed record CreateCiRelationshipRequest(
    Guid TargetCiId,
    string RelationshipType,
    string? Notes);

public sealed record CreateBusinessServiceRequest(
    string Name,
    string Criticality,
    string? Description,
    Guid? OwnerUserId,
    int? RtoMinutes,
    int? RpoMinutes);

public sealed record UpdateBusinessServiceRequest(
    string Name,
    string Criticality,
    string? Description,
    Guid? OwnerUserId,
    int? RtoMinutes,
    int? RpoMinutes,
    bool IsActive);

public static class AssetEndpoints
{
    public const string AssetsRead = "assets.read";
    public const string AssetsManage = "assets.manage";

    public static IEndpointRouteBuilder MapAssetEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder readGroup = endpoints.MapGroup("/api/v1/assets")
            .RequirePermission(AssetsRead);

        readGroup.MapGet(string.Empty, async (
            string? search,
            AssetService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(search, cancellationToken)));

        readGroup.MapGet("/{id:guid}", async (
            Guid id,
            AssetService service,
            CancellationToken cancellationToken) =>
        {
            AssetDto? item = await service.GetAsync(id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        readGroup.MapGet("/{id:guid}/assignments", async (
            Guid id,
            AssetService service,
            CancellationToken cancellationToken) =>
        {
            if (await service.GetAsync(id, cancellationToken) is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(await service.ListAssignmentsAsync(id, cancellationToken));
        });

        RouteGroupBuilder manageGroup = endpoints.MapGroup("/api/v1/assets")
            .RequirePermission(AssetsManage);

        manageGroup.MapPost(string.Empty, async (
            CreateAssetRequest request,
            AssetService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.AssetType) || string.IsNullOrWhiteSpace(request.Name))
            {
                return CmdbEndpoints.ValidationProblem("assetType and name are required.");
            }

            try
            {
                Asset created = await service.CreateAsync(
                    request.AssetType,
                    request.Name,
                    request.ConfigurationItemId,
                    request.SerialNumber,
                    request.Manufacturer,
                    request.Model,
                    request.PurchaseDate,
                    request.PurchaseCost,
                    request.WarrantyExpiry,
                    request.LocationId,
                    request.Notes,
                    cancellationToken);
                return Results.Created($"/api/v1/assets/{created.Id}", await service.GetAsync(created.Id, cancellationToken));
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

        manageGroup.MapPut("/{id:guid}", async (
            Guid id,
            UpdateAssetRequest request,
            AssetService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.AssetType)
                || string.IsNullOrWhiteSpace(request.Name)
                || string.IsNullOrWhiteSpace(request.Status)
                || !Enum.TryParse(request.Status, ignoreCase: true, out AssetStatus status))
            {
                return CmdbEndpoints.ValidationProblem("assetType, name, and a valid status are required.");
            }

            try
            {
                await service.UpdateAsync(
                    id,
                    request.AssetType,
                    request.Name,
                    status,
                    request.ConfigurationItemId,
                    request.SerialNumber,
                    request.Manufacturer,
                    request.Model,
                    request.PurchaseDate,
                    request.PurchaseCost,
                    request.WarrantyExpiry,
                    request.LocationId,
                    request.Notes,
                    request.RowVersion ?? string.Empty,
                    cancellationToken);
                return Results.Ok(await service.GetAsync(id, cancellationToken));
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

        manageGroup.MapPost("/{id:guid}/assign", async (
            Guid id,
            AssignAssetRequest request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            AssetService service,
            CancellationToken cancellationToken) =>
        {
            if (request.AssignedToUserId == Guid.Empty)
            {
                return CmdbEndpoints.ValidationProblem("assignedToUserId is required.");
            }

            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return Results.Json(
                    new { error = new { code = "session_unavailable", message = "No active ITMG user session." } },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            try
            {
                AssetAssignment assignment = await service.AssignAsync(
                    id,
                    request.AssignedToUserId,
                    session.Id,
                    request.Notes,
                    cancellationToken);
                return Results.Ok(new AssetAssignmentDto(
                    assignment.Id,
                    assignment.AssetId,
                    assignment.AssignedToUserId,
                    assignment.AssignedByUserId,
                    assignment.AssignedAtUtc,
                    assignment.ReturnedAtUtc,
                    assignment.Notes,
                    assignment.ReturnedAtUtc == null));
            }
            catch (InvalidOperationException ex)
            {
                return CmdbEndpoints.FromDomainError(ex);
            }
        });

        manageGroup.MapPost("/{id:guid}/return", async (
            Guid id,
            ReturnAssetRequest? request,
            AssetService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                AssetAssignment assignment = await service.ReturnAsync(id, request?.Notes, cancellationToken);
                return Results.Ok(new AssetAssignmentDto(
                    assignment.Id,
                    assignment.AssetId,
                    assignment.AssignedToUserId,
                    assignment.AssignedByUserId,
                    assignment.AssignedAtUtc,
                    assignment.ReturnedAtUtc,
                    assignment.Notes,
                    assignment.ReturnedAtUtc == null));
            }
            catch (InvalidOperationException ex)
            {
                return CmdbEndpoints.FromDomainError(ex);
            }
        });

        return endpoints;
    }
}

public sealed record CreateAssetRequest(
    string AssetType,
    string Name,
    Guid? ConfigurationItemId,
    string? SerialNumber,
    string? Manufacturer,
    string? Model,
    DateOnly? PurchaseDate,
    decimal? PurchaseCost,
    DateOnly? WarrantyExpiry,
    Guid? LocationId,
    string? Notes);

public sealed record UpdateAssetRequest(
    string AssetType,
    string Name,
    string Status,
    Guid? ConfigurationItemId,
    string? SerialNumber,
    string? Manufacturer,
    string? Model,
    DateOnly? PurchaseDate,
    decimal? PurchaseCost,
    DateOnly? WarrantyExpiry,
    Guid? LocationId,
    string? Notes,
    string? RowVersion);

public sealed record AssignAssetRequest(Guid AssignedToUserId, string? Notes);

public sealed record ReturnAssetRequest(string? Notes);

/// <summary>
/// Current-user equipment only. Does not grant assets.read; ownership is session-scoped.
/// </summary>
public static class MeEquipmentEndpoints
{
    public static IEndpointRouteBuilder MapMeEquipmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/me/equipment", async (
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            AssetService assets,
            CancellationToken cancellationToken) =>
        {
            if (principal.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return Results.Json(
                    new { error = new { code = "session_unavailable", message = "No active ITMG user session." } },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            IReadOnlyList<AssetDto> items =
                await assets.ListActiveEquipmentForUserAsync(session.Id, cancellationToken);
            return Results.Ok(items);
        }).RequireAuthorization();

        return endpoints;
    }
}
