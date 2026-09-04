using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.ServiceDesk.Domain;
using Qec.Itmg.ServiceDesk.Services;
using TicketEntity = Qec.Itmg.ServiceDesk.Domain.Ticket;

namespace Qec.Itmg.Host.ServiceDesk;

public static class TicketEndpoints
{
    public const string TicketsRead = "tickets.read";
    public const string TicketsManage = "tickets.manage";
    public const string IncidentsSecurity = "incidents.security";

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
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService service,
            CancellationToken cancellationToken) =>
        {
            bool includeSecurity = await HasIncidentsSecurityAsync(principal, currentUser, cancellationToken);
            TicketListResult result = await service.ListAsync(
                page ?? 1,
                pageSize ?? 25,
                search,
                ParseEnum<TicketStatus>(status),
                ParseEnum<TicketType>(type),
                ParseEnum<TicketPriority>(priority),
                includeSecurityClassification: includeSecurity,
                cancellationToken: cancellationToken);
            return Results.Ok(result);
        });

        // Static paths before {id} route
        readGroup.MapGet("/queues", async (TicketService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListQueuesAsync(cancellationToken)));

        readGroup.MapGet("/dashboard", async (
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService service,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return Results.Json(
                    new { error = new { code = "session_unavailable", message = "No active ITMG user session." } },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return Results.Ok(await service.GetDashboardAsync(session.Id, cancellationToken));
        });

        readGroup.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService service,
            CancellationToken cancellationToken) =>
        {
            bool includeSecurity = await HasIncidentsSecurityAsync(principal, currentUser, cancellationToken);
            TicketDto? ticket = await service.GetAsync(id, includeSecurity, cancellationToken);
            return ticket is null ? Results.NotFound() : Results.Ok(ticket);
        });

        RouteGroupBuilder manageGroup = endpoints.MapGroup("/api/v1/tickets")
            .RequirePermission(TicketsManage);

        manageGroup.MapPost(string.Empty, async (
            CreateTicketRequest request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService service,
            TicketNotificationService notifications,
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
                TicketEntity created = await service.CreateAsync(
                    type,
                    request.Title,
                    request.Description,
                    requesterId,
                    priority,
                    request.ConfigurationItemId,
                    request.Category,
                    request.QueueId,
                    cancellationToken);
                TicketDto? dto = await service.GetAsync(created.Id, cancellationToken: cancellationToken);
                if (dto is not null)
                {
                    await notifications.NotifyTicketCreatedAsync(dto, cancellationToken);
                }

                return Results.Created($"/api/v1/tickets/{created.Id}", dto);
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
                return Results.Ok(await service.GetAsync(id, cancellationToken: cancellationToken));
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
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService service,
            TicketNotificationService notifications,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Status)
                || !Enum.TryParse(request.Status, ignoreCase: true, out TicketStatus status))
            {
                return ValidationProblem("A valid status is required.");
            }

            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            TicketDto? before = await service.GetAsync(id, cancellationToken: cancellationToken);
            if (before is null)
            {
                return Results.NotFound();
            }

            try
            {
                await service.ChangeStatusAsync(id, status, session.Id, request.RowVersion, cancellationToken);
                TicketDto? after = await service.GetAsync(id, cancellationToken: cancellationToken);
                if (after is not null)
                {
                    await notifications.NotifyStatusChangedAsync(after, before.Status, cancellationToken);
                }

                return Results.Ok(after);
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
            TicketNotificationService notifications,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            TicketDto? before = await service.GetAsync(id, cancellationToken: cancellationToken);
            if (before is null)
            {
                return Results.NotFound();
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
                TicketDto? after = await service.GetAsync(id, cancellationToken: cancellationToken);
                if (after is not null)
                {
                    await notifications.NotifyAssignedAsync(after, before.AssignedUserId, cancellationToken);
                    if (!string.Equals(before.Status, after.Status, StringComparison.Ordinal))
                    {
                        await notifications.NotifyStatusChangedAsync(after, before.Status, cancellationToken);
                    }
                }

                return Results.Ok(after);
            }
            catch (InvalidOperationException ex)
            {
                return FromDomainError(ex);
            }
        });

        manageGroup.MapPut("/{id:guid}/incident", async (
            Guid id,
            UpdateIncidentRequest request,
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

            bool canSecurity = HasPermission(session, IncidentsSecurity);
            bool updateSecurity = !string.IsNullOrWhiteSpace(request.SecurityClassification);
            SecurityClassification? classification = null;
            if (updateSecurity)
            {
                if (!canSecurity)
                {
                    return Results.Json(
                        new
                        {
                            error = new
                            {
                                code = "permission_denied",
                                message = "incidents.security is required to change security classification.",
                            },
                        },
                        statusCode: StatusCodes.Status403Forbidden);
                }

                if (!Enum.TryParse(request.SecurityClassification, ignoreCase: true, out SecurityClassification parsed)
                    || !Enum.IsDefined(parsed))
                {
                    return ValidationProblem("A valid securityClassification is required.");
                }

                classification = parsed;
            }

            try
            {
                await service.UpdateIncidentAsync(
                    id,
                    request.IsMajorIncident,
                    classification,
                    updateSecurity,
                    request.RowVersion ?? string.Empty,
                    cancellationToken);
                return Results.Ok(await service.GetAsync(id, canSecurity, cancellationToken));
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

    public static IEndpointRouteBuilder MapMeTicketEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/me/tickets", async (
            CreateMeTicketRequest request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService service,
            TicketNotificationService notifications,
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
                TicketEntity created = await service.CreateAsync(
                    type,
                    request.Title,
                    request.Description,
                    session.Id,
                    priority,
                    request.ConfigurationItemId,
                    request.Category,
                    cancellationToken: cancellationToken);
                TicketDto? dto = await service.GetForRequesterAsync(created.Id, session.Id, cancellationToken);
                if (dto is not null)
                {
                    await notifications.NotifyTicketCreatedAsync(dto, cancellationToken);
                }

                return Results.Created($"/api/v1/me/tickets/{created.Id}", dto);
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

    private static async Task<bool> HasIncidentsSecurityAsync(
        ClaimsPrincipal principal,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
        return session is not null && HasPermission(session, IncidentsSecurity);
    }

    private static bool HasPermission(CurrentUserDto session, string permissionKey) =>
        session.Permissions.Any(item => string.Equals(item, permissionKey, StringComparison.OrdinalIgnoreCase));

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

public sealed record UpdateIncidentRequest(
    bool IsMajorIncident,
    string? SecurityClassification,
    string? RowVersion);

public sealed record ChangeTicketStatusRequest(string Status, string? RowVersion);

public sealed record AssignTicketRequest(Guid? QueueId, Guid? AssignedUserId, string? Notes);
