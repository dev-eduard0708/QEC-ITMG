using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.ServiceDesk.Domain;
using Qec.Itmg.ServiceDesk.Services;

namespace Qec.Itmg.Host.ServiceDesk;

public static class TicketEndpoints
{
    public const string TicketsRead = "tickets.read";
    public const string TicketsManage = "tickets.manage";

    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder readGroup = endpoints.MapGroup("/api/v1/tickets")
            .RequirePermission(TicketsRead);

        readGroup.MapGet(string.Empty, async (
            int? page,
            int? pageSize,
            string? search,
            string? status,
            string? type,
            string? priority,
            TicketService service,
            CancellationToken cancellationToken) =>
        {
            TicketListResult result = await service.ListAsync(
                page ?? 1,
                pageSize ?? 25,
                search,
                ParseEnum<TicketStatus>(status),
                ParseEnum<TicketType>(type),
                ParseEnum<TicketPriority>(priority),
                cancellationToken: cancellationToken);
            return Results.Ok(result);
        });

        // Queues before {id} route
        readGroup.MapGet("/queues", async (TicketService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListQueuesAsync(cancellationToken)));

        readGroup.MapGet("/{id:guid}", async (
            Guid id,
            TicketService service,
            CancellationToken cancellationToken) =>
        {
            TicketDto? ticket = await service.GetAsync(id, cancellationToken);
            return ticket is null ? Results.NotFound() : Results.Ok(ticket);
        });

        RouteGroupBuilder manageGroup = endpoints.MapGroup("/api/v1/tickets")
            .RequirePermission(TicketsManage);

        manageGroup.MapPost(string.Empty, async (
            CreateTicketRequest request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            {
                return ValidationProblem("title and description are required.");
            }

            if (string.IsNullOrWhiteSpace(request.Type)
                || !Enum.TryParse(request.Type, ignoreCase: true, out TicketType type))
            {
                return ValidationProblem("A valid type is required.");
            }

            TicketPriority priority = TicketPriority.Medium;
            if (!string.IsNullOrWhiteSpace(request.Priority)
                && !Enum.TryParse(request.Priority, ignoreCase: true, out priority))
            {
                return ValidationProblem("Invalid priority.");
            }

            Guid requesterId = request.RequesterUserId ?? Guid.Empty;
            if (requesterId == Guid.Empty)
            {
                CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
                if (session is null)
                {
                    return SessionUnavailable();
                }

                requesterId = session.Id;
            }

            try
            {
                Ticket created = await service.CreateAsync(
                    type,
                    request.Title,
                    request.Description,
                    requesterId,
                    priority,
                    request.ConfigurationItemId,
                    request.Category,
                    request.QueueId,
                    cancellationToken);
                return Results.Created(
                    $"/api/v1/tickets/{created.Id}",
                    await service.GetAsync(created.Id, cancellationToken));
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

        manageGroup.MapPut("/{id:guid}", async (
            Guid id,
            UpdateTicketRequest request,
            TicketService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            {
                return ValidationProblem("title and description are required.");
            }

            TicketPriority priority = TicketPriority.Medium;
            if (string.IsNullOrWhiteSpace(request.Priority)
                || !Enum.TryParse(request.Priority, ignoreCase: true, out priority))
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
                    request.ConfigurationItemId,
                    request.Category,
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

        manageGroup.MapPost("/{id:guid}/status", async (
            Guid id,
            ChangeTicketStatusRequest request,
            TicketService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Status)
                || !Enum.TryParse(request.Status, ignoreCase: true, out TicketStatus status))
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

        manageGroup.MapPost("/{id:guid}/assign", async (
            Guid id,
            AssignTicketRequest request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService service,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            try
            {
                await service.AssignAsync(
                    id,
                    session.Id,
                    request.QueueId,
                    request.AssignedUserId,
                    request.Notes,
                    cancellationToken);
                return Results.Ok(await service.GetAsync(id, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return FromDomainError(ex);
            }
        });

        return endpoints;
    }

    public static IEndpointRouteBuilder MapMeTicketEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/me/tickets", async (
            CreateMeTicketRequest request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService service,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await ResolveSessionAsync(principal, currentUser, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            {
                return ValidationProblem("title and description are required.");
            }

            TicketType type = TicketType.ServiceRequest;
            if (!string.IsNullOrWhiteSpace(request.Type)
                && !Enum.TryParse(request.Type, ignoreCase: true, out type))
            {
                return ValidationProblem("Invalid type.");
            }

            TicketPriority priority = TicketPriority.Medium;
            if (!string.IsNullOrWhiteSpace(request.Priority)
                && !Enum.TryParse(request.Priority, ignoreCase: true, out priority))
            {
                return ValidationProblem("Invalid priority.");
            }

            try
            {
                Ticket created = await service.CreateAsync(
                    type,
                    request.Title,
                    request.Description,
                    session.Id,
                    priority,
                    request.ConfigurationItemId,
                    request.Category,
                    cancellationToken: cancellationToken);
                return Results.Created(
                    $"/api/v1/me/tickets/{created.Id}",
                    await service.GetForRequesterAsync(created.Id, session.Id, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return ValidationProblem(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return FromDomainError(ex);
            }
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/me/tickets", async (
            int? page,
            int? pageSize,
            string? search,
            string? status,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService service,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await ResolveSessionAsync(principal, currentUser, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            TicketListResult result = await service.ListAsync(
                page ?? 1,
                pageSize ?? 25,
                search,
                ParseEnum<TicketStatus>(status),
                requesterUserId: session.Id,
                cancellationToken: cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/me/tickets/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService service,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await ResolveSessionAsync(principal, currentUser, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            TicketDto? ticket = await service.GetForRequesterAsync(id, session.Id, cancellationToken);
            return ticket is null ? Results.NotFound() : Results.Ok(ticket);
        }).RequireAuthorization();

        return endpoints;
    }

    private static async Task<CurrentUserDto?> ResolveSessionAsync(
        ClaimsPrincipal principal,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return await currentUser.GetSessionAsync(principal, cancellationToken);
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

public sealed record CreateTicketRequest(
    string Type,
    string Title,
    string Description,
    string? Priority,
    Guid? RequesterUserId,
    Guid? ConfigurationItemId,
    string? Category,
    Guid? QueueId);

public sealed record CreateMeTicketRequest(
    string? Type,
    string Title,
    string Description,
    string? Priority,
    Guid? ConfigurationItemId,
    string? Category);

public sealed record UpdateTicketRequest(
    string Title,
    string Description,
    string Priority,
    Guid? ConfigurationItemId,
    string? Category,
    string? RowVersion);

public sealed record ChangeTicketStatusRequest(string Status, string? RowVersion);

public sealed record AssignTicketRequest(Guid? QueueId, Guid? AssignedUserId, string? Notes);
