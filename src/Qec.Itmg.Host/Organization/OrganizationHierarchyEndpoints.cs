using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Contracts.Identity;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Organization.Domain;
using Qec.Itmg.Organization.Services;

namespace Qec.Itmg.Host.Organization;

public static class OrganizationHierarchyEndpoints
{
    public const string ReadPermission = "organization.hierarchy.read";
    public const string ManagePermission = "organization.hierarchy.manage";

    public static IEndpointRouteBuilder MapOrganizationHierarchyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/organization");

        group.MapGet("/departments", async (DepartmentService departments, CancellationToken ct) =>
                Results.Ok(await departments.ListDetailedAsync(activeOnly: false, ct)))
            .RequireAnyPermission(ReadPermission, ManagePermission, "admin.lookups");

        group.MapGet("/departments/company-view", async (DepartmentService departments, CancellationToken ct) =>
                Results.Ok(await departments.CompanyViewAsync(ct)))
            .RequireAnyPermission(ReadPermission, ManagePermission);

        group.MapGet("/departments/{id:guid}", async (Guid id, DepartmentService departments, CancellationToken ct) =>
            {
                DepartmentDetailDto? item = await departments.GetAsync(id, ct);
                return item is null ? Results.NotFound() : Results.Ok(item);
            })
            .RequireAnyPermission(ReadPermission, ManagePermission);

        group.MapPost("/departments", async (
                CreateDepartmentRequest request,
                DepartmentService departments,
                CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await departments.CreateAsync(
                        request.NameEn,
                        request.NameAr,
                        request.Code,
                        request.DescriptionEn,
                        request.DescriptionAr,
                        request.ParentDepartmentId,
                        request.SortOrder,
                        request.UnitType ?? DepartmentUnitType.Department,
                        ct));
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status400BadRequest);
                }
            })
            .RequirePermission(ManagePermission);

        group.MapPut("/departments/{id:guid}", async (
                Guid id,
                UpdateDepartmentRequest request,
                DepartmentService departments,
                CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await departments.UpdateAsync(
                        id,
                        request.NameEn,
                        request.NameAr,
                        request.Code,
                        request.DescriptionEn,
                        request.DescriptionAr,
                        request.ParentDepartmentId,
                        request.SortOrder,
                        request.IsActive,
                        request.UnitType ?? DepartmentUnitType.Department,
                        ct));
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status400BadRequest);
                }
            })
            .RequirePermission(ManagePermission);

        group.MapPost("/departments/{id:guid}/move", async (
                Guid id,
                MoveDepartmentRequest request,
                DepartmentService departments,
                CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await departments.MoveAsync(id, request.ParentDepartmentId, ct));
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status400BadRequest);
                }
            })
            .RequirePermission(ManagePermission);

        group.MapPost("/departments/{id:guid}/deactivate", async (
                Guid id,
                DepartmentService departments,
                CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await departments.DeactivateAsync(id, ct));
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status400BadRequest);
                }
            })
            .RequirePermission(ManagePermission);

        group.MapPost("/departments/{id:guid}/reactivate", async (
                Guid id,
                DepartmentService departments,
                CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await departments.ReactivateAsync(id, ct));
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status400BadRequest);
                }
            })
            .RequirePermission(ManagePermission);

        group.MapGet("/departments/{id:guid}/members", async (
                Guid id,
                DepartmentService departments,
                CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await departments.ListMembersAsync(id, ct));
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status404NotFound);
                }
            })
            .RequireAnyPermission(ReadPermission, ManagePermission);

        group.MapPost("/departments/{id:guid}/members", async (
                Guid id,
                AddDepartmentMemberRequest request,
                DepartmentService departments,
                CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await departments.AddMemberAsync(id, request.UserId, request.IsPrimary ?? false, ct));
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status400BadRequest);
                }
            })
            .RequirePermission(ManagePermission);

        group.MapPost("/departments/{id:guid}/members/batch", async (
                Guid id,
                AddDepartmentMembersBatchRequest request,
                DepartmentService departments,
                CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await departments.AddMembersBatchAsync(
                        id,
                        request.UserIds,
                        request.IsPrimary ?? false,
                        ct));
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status400BadRequest);
                }
            })
            .RequirePermission(ManagePermission);

        group.MapDelete("/departments/{id:guid}/members/{userId:guid}", async (
                Guid id,
                Guid userId,
                DepartmentService departments,
                CancellationToken ct) =>
            {
                try
                {
                    await departments.RemoveMemberAsync(id, userId, ct);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status400BadRequest);
                }
            })
            .RequirePermission(ManagePermission);

        group.MapPost("/departments/{id:guid}/members/{userId:guid}/primary", async (
                Guid id,
                Guid userId,
                DepartmentService departments,
                CancellationToken ct) =>
            {
                try
                {
                    await departments.SetPrimaryAsync(id, userId, ct);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return ErrorResult(ex.Message, StatusCodes.Status400BadRequest);
                }
            })
            .RequirePermission(ManagePermission);

        group.MapGet("/people", async (
                string? search,
                Guid? departmentId,
                bool? activeOnly,
                DepartmentService departments,
                CancellationToken ct) =>
                Results.Ok(await departments.ListPeopleAsync(search, departmentId, activeOnly, ct)))
            .RequireAnyPermission(ReadPermission, ManagePermission);

        group.MapGet("/users/{userId:guid}/profile-summary", async (
                Guid userId,
                DepartmentService departments,
                CancellationToken ct) =>
            {
                UserOrgProfileDto? profile = await departments.GetProfileSummaryAsync(userId, ct);
                return profile is null ? Results.NotFound() : Results.Ok(profile);
            })
            .RequireAnyPermission(ReadPermission, ManagePermission);

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
                    return Results.Ok(await service.AssignUserAsync(
                        id,
                        request.UserId,
                        request.IsPrimary ?? false,
                        ct,
                        addToDepartmentIfMissing: request.AddToDepartment ?? false));
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
                Guid? departmentId,
                bool? searchAll,
                PositionService service,
                IActiveEmployeeLookup employees,
                CancellationToken ct) =>
                Results.Ok(await service.SearchActiveUsersAsync(
                    search,
                    employees,
                    ct,
                    departmentId,
                    searchAll ?? false)))
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

public sealed record CreateDepartmentRequest(
    string NameEn,
    string? NameAr,
    string Code,
    string? DescriptionEn,
    string? DescriptionAr,
    Guid? ParentDepartmentId,
    int SortOrder,
    DepartmentUnitType? UnitType = null);

public sealed record UpdateDepartmentRequest(
    string NameEn,
    string? NameAr,
    string Code,
    string? DescriptionEn,
    string? DescriptionAr,
    Guid? ParentDepartmentId,
    int SortOrder,
    bool IsActive,
    DepartmentUnitType? UnitType = null);

public sealed record MoveDepartmentRequest(Guid? ParentDepartmentId);

public sealed record AddDepartmentMemberRequest(Guid UserId, bool? IsPrimary);

public sealed record AddDepartmentMembersBatchRequest(IReadOnlyList<Guid> UserIds, bool? IsPrimary);

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

public sealed record AssignUserRequest(Guid UserId, bool? IsPrimary, bool? AddToDepartment);
