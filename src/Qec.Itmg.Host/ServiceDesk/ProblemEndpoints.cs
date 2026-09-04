using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.ServiceDesk.Domain;
using Qec.Itmg.ServiceDesk.Services;

namespace Qec.Itmg.Host.ServiceDesk;

public static class ProblemEndpoints
{
    public const string ProblemsRead = "problems.read";
    public const string ProblemsManage = "problems.manage";

    public static IEndpointRouteBuilder MapProblemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder readGroup = endpoints.MapGroup("/api/v1/problems")
            .RequirePermission(ProblemsRead);

        readGroup.MapGet(string.Empty, async (
            int? page,
            int? pageSize,
            string? search,
            string? status,
            string? priority,
            ProblemService service,
            CancellationToken cancellationToken) =>
        {
            ProblemListResult result = await service.ListAsync(
                page ?? 1,
                pageSize ?? 25,
                search,
                ParseEnum<ProblemStatus>(status),
                ParseEnum<TicketPriority>(priority),
                cancellationToken);
            return Results.Ok(result);
        });

        // Static paths before {id}
        readGroup.MapGet("/recurring-groups", async (
            int? take,
            ProblemService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListTopRecurringGroupsAsync(take ?? 10, cancellationToken)));

        readGroup.MapGet("/{id:guid}", async (
            Guid id,
            ProblemService service,
            CancellationToken cancellationToken) =>
        {
            ProblemDto? problem = await service.GetAsync(id, cancellationToken);
            return problem is null ? Results.NotFound() : Results.Ok(problem);
        });

        readGroup.MapGet("/{id:guid}/metrics", async (
            Guid id,
            int? recentDays,
            ProblemService service,
            CancellationToken cancellationToken) =>
        {
            ProblemRecurringMetricsDto? metrics = await service.GetRecurringMetricsAsync(
                id,
                recentDays ?? 30,
                cancellationToken);
            return metrics is null ? Results.NotFound() : Results.Ok(metrics);
        });

        readGroup.MapGet("/{id:guid}/incidents", async (
            Guid id,
            ProblemService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await service.ListIncidentsAsync(id, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return FromDomainError(ex);
            }
        });

        RouteGroupBuilder manageGroup = endpoints.MapGroup("/api/v1/problems")
            .RequirePermission(ProblemsManage);

        manageGroup.MapPost(string.Empty, async (
            CreateProblemRequest request,
            ProblemService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            {
                return ValidationProblem("title and description are required.");
            }

            TicketPriority priority = TicketPriority.Medium;
            if (!string.IsNullOrWhiteSpace(request.Priority)
                && !Enum.TryParse(request.Priority, ignoreCase: true, out priority))
            {
                return ValidationProblem("Invalid priority.");
            }

            try
            {
                var created = await service.CreateAsync(
                    request.Title,
                    request.Description,
                    priority,
                    request.OwnerUserId,
                    request.ConfigurationItemId,
                    cancellationToken);
                return Results.Created(
                    $"/api/v1/problems/{created.Id}",
                    await service.GetAsync(created.Id, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return ValidationProblem(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return FromDomainError(ex);
            }
        });

        manageGroup.MapPut("/{id:guid}", async (
            Guid id,
            UpdateProblemRequest request,
            ProblemService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            {
                return ValidationProblem("title and description are required.");
            }

            if (string.IsNullOrWhiteSpace(request.Priority)
                || !Enum.TryParse(request.Priority, ignoreCase: true, out TicketPriority priority))
            {
                return ValidationProblem("A valid priority is required.");
            }

            try
            {
                await service.UpdateAsync(
                    id,
                    request.Title,
                    request.Description,
                    priority,
                    request.OwnerUserId,
                    request.ConfigurationItemId,
                    request.RootCause,
                    request.Workaround,
                    request.RowVersion ?? string.Empty,
                    cancellationToken);
                return Results.Ok(await service.GetAsync(id, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return ValidationProblem(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return FromDomainError(ex);
            }
        });

        manageGroup.MapPost("/{id:guid}/status", async (
            Guid id,
            ChangeProblemStatusRequest request,
            ProblemService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Status)
                || !Enum.TryParse(request.Status, ignoreCase: true, out ProblemStatus status))
            {
                return ValidationProblem("A valid status is required.");
            }

            try
            {
                await service.ChangeStatusAsync(id, status, request.RowVersion, cancellationToken);
                return Results.Ok(await service.GetAsync(id, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return FromDomainError(ex);
            }
        });

        manageGroup.MapPost("/{id:guid}/known-error", async (
            Guid id,
            SetKnownErrorRequest request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            ProblemService service,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            try
            {
                await service.SetKnownErrorAsync(
                    id,
                    request.IsKnownError,
                    session.Id,
                    request.RowVersion ?? string.Empty,
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

        manageGroup.MapPost("/{id:guid}/incidents", async (
            Guid id,
            LinkProblemIncidentRequest request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            ProblemService service,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            if (request.IncidentTicketId == Guid.Empty)
            {
                return ValidationProblem("incidentTicketId is required.");
            }

            try
            {
                await service.LinkIncidentAsync(id, request.IncidentTicketId, session.Id, cancellationToken);
                return Results.Ok(await service.ListIncidentsAsync(id, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return FromDomainError(ex);
            }
        });

        manageGroup.MapDelete("/{id:guid}/incidents/{ticketId:guid}", async (
            Guid id,
            Guid ticketId,
            ProblemService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await service.UnlinkIncidentAsync(id, ticketId, cancellationToken);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return FromDomainError(ex);
            }
        });

        return endpoints;
    }

    /// <summary>Related problems for an IT ticket detail (tickets.read).</summary>
    public static IEndpointRouteBuilder MapTicketRelatedProblemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/tickets/{id:guid}/problems", async (
            Guid id,
            ProblemService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListProblemsForIncidentAsync(id, cancellationToken)))
            .RequirePermission(TicketEndpoints.TicketsRead);

        return endpoints;
    }

    private static IResult SessionUnavailable() =>
        Results.Json(
            new { error = new { code = "session_unavailable", message = "No active ITMG user session." } },
            statusCode: StatusCodes.Status403Forbidden);

    private static IResult ValidationProblem(string message) =>
        Results.Json(
            new { error = new { code = "validation_error", message } },
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult FromDomainError(InvalidOperationException ex)
    {
        string message = ex.Message;
        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(
                new { error = new { code = "not_found", message } },
                statusCode: StatusCodes.Status404NotFound);
        }

        if (message.Contains("modified by another", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Cannot transition", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(
                new { error = new { code = "conflict", message } },
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Json(
            new { error = new { code = "invalid_operation", message } },
            statusCode: StatusCodes.Status400BadRequest);
    }

    private static TEnum? ParseEnum<TEnum>(string? value)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse(value, ignoreCase: true, out TEnum parsed) ? parsed : null;
    }
}

public sealed record CreateProblemRequest(
    string Title,
    string Description,
    string? Priority,
    Guid? OwnerUserId,
    Guid? ConfigurationItemId);

public sealed record UpdateProblemRequest(
    string Title,
    string Description,
    string Priority,
    Guid? OwnerUserId,
    Guid? ConfigurationItemId,
    string? RootCause,
    string? Workaround,
    string? RowVersion);

public sealed record ChangeProblemStatusRequest(string Status, string? RowVersion);

public sealed record SetKnownErrorRequest(bool IsKnownError, string? RowVersion);

public sealed record LinkProblemIncidentRequest(Guid IncidentTicketId);
