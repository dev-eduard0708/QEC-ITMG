using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Contracts.Identity;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Organization.Services;

namespace Qec.Itmg.Host.Organization;

public static class OrganizationHierarchyEndpoints
{
    public const string ReadPermission = "organization.hierarchy.read";
    public const string ManagePermission = "organization.hierarchy.manage";

    public static IEndpointRouteBuilder MapOrganizationHierarchyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/organization");

        group.MapGet("/departments", async (PositionService service, CancellationToken ct) =>
                Results.Ok(await service.ListDepartmentsAsync(ct)))
            .RequireAnyPermission(ReadPermission, ManagePermission, "admin.lookups");

        group.MapGet("/positions", async (
                Guid? departmentId,
                bool? activeOnly,
                PositionService service,
                CancellationToken ct) =>
                Results.Ok(await service.ListPositionsAsync(departmentId, activeOnly ?? false, ct)))
            .RequireAnyPermission(ReadPermission, ManagePermission);

        group.MapGet("/positions/hierarchy", async (
                Guid? departmentId,
                PositionService service,
                CancellationToken ct) =>
            {
                PositionHierarchyResult result = await service.GetHierarchyAsync(departmentId, ct);
                return Results.Ok(new
                {
                    departmentId = result.DepartmentId,
                    departmentName = result.DepartmentName,
                    roots = result.Roots,
                });
            })
            .RequireAnyPermission(ReadPermission, ManagePermission);

        group.MapPost("/positions", async (
                CreatePositionRequest request,
                PositionService service,
                CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await service.CreateAsync(
                        request.DepartmentId,
                        request.Key,
                        request.NameEn,
                        request.NameAr,
                        request.ParentPositionId,
                        request.DescriptionEn,
                        request.DescriptionAr,
                        request.IsManagerial,
                        request.SortOrder,
                        request.IsActive,
                        ct));
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status400BadRequest);
                }
            })
            .RequirePermission(ManagePermission);

        group.MapPut("/positions/{id:guid}", async (
                Guid id,
                UpdatePositionRequest request,
                PositionService service,
                CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await service.UpdateAsync(
                        id,
                        request.NameEn,
                        request.NameAr,
                        request.DescriptionEn,
                        request.DescriptionAr,
                        request.IsManagerial,
                        request.SortOrder,
                        request.IsActive,
                        ct));
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status400BadRequest);
                }
            })
            .RequirePermission(ManagePermission);

        group.MapPost("/positions/{id:guid}/parent", async (
                Guid id,
                ChangeParentRequest request,
                PositionService service,
                CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await service.ChangeParentAsync(id, request.ParentPositionId, ct));
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status400BadRequest);
                }
            })
            .RequirePermission(ManagePermission);

        group.MapPost("/positions/{id:guid}/deactivate", async (
                Guid id,
                PositionService service,
                CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await service.DeactivateAsync(id, ct));
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status400BadRequest);
                }
            })
            .RequirePermission(ManagePermission);

        group.MapGet("/positions/{id:guid}/assignments", async (
                Guid id,
                PositionService service,
                CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await service.ListAssignmentsAsync(id, ct));
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status404NotFound);
                }
            })
            .RequireAnyPermission(ReadPermission, ManagePermission);

        group.MapPost("/positions/{id:guid}/assignments", async (
                Guid id,
                AssignUserRequest request,
                PositionService service,
                CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await service.AssignUserAsync(id, request.UserId, request.IsPrimary ?? false, ct));
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status400BadRequest);
                }
            })
            .RequirePermission(ManagePermission);

        group.MapDelete("/positions/{id:guid}/assignments/{assignmentId:guid}", async (
                Guid id,
                Guid assignmentId,
                PositionService service,
                CancellationToken ct) =>
            {
                try
                {
                    await service.RemoveAssignmentAsync(id, assignmentId, ct);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status404NotFound);
                }
            })
            .RequirePermission(ManagePermission);

        group.MapPost("/positions/{id:guid}/assignments/{assignmentId:guid}/primary", async (
                Guid id,
                Guid assignmentId,
                PositionService service,
                CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await service.SetPrimaryAsync(id, assignmentId, ct));
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status400BadRequest);
                }
            })
            .RequirePermission(ManagePermission);

        group.MapGet("/users/{userId:guid}/positions", async (
                Guid userId,
                PositionService service,
                CancellationToken ct) =>
                Results.Ok(await service.ListUserPositionsAsync(userId, ct)))
            .RequireAnyPermission(ReadPermission, ManagePermission);

        group.MapGet("/active-users", async (
                string? search,
                PositionService service,
                IActiveEmployeeLookup employees,
                CancellationToken ct) =>
                Results.Ok(await service.SearchActiveUsersAsync(search, employees, ct)))
            .RequireAnyPermission(ReadPermission, ManagePermission);

        return endpoints;
    }

    private static IResult ErrorResult(string message, int statusCode) =>
        Results.Json(
            new
            {
                error = new
                {
                    code = "organization.hierarchy",
                    message,
                    details = Array.Empty<object>(),
                },
            },
            statusCode: statusCode);
}

public sealed record CreatePositionRequest(
    Guid DepartmentId,
    string Key,
    string NameEn,
    string NameAr,
    Guid? ParentPositionId,
    string? DescriptionEn,
    string? DescriptionAr,
    bool IsManagerial,
    int SortOrder,
    bool IsActive = true);

public sealed record UpdatePositionRequest(
    string NameEn,
    string NameAr,
    string? DescriptionEn,
    string? DescriptionAr,
    bool IsManagerial,
    int SortOrder,
    bool IsActive);

public sealed record ChangeParentRequest(Guid? ParentPositionId);

public sealed record AssignUserRequest(Guid UserId, bool? IsPrimary);
