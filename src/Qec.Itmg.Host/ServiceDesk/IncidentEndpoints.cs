using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Operations.Services;
using Qec.Itmg.ServiceDesk.Domain;
using Qec.Itmg.ServiceDesk.Services;
using TicketEntity = Qec.Itmg.ServiceDesk.Domain.Ticket;

namespace Qec.Itmg.Host.ServiceDesk;

/// <summary>
/// Legacy promote path. Prefer POST /api/v1/events/{id}/promote which validates the OperationalEvent and updates its status.
/// </summary>
public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/incidents/promote-from-event", async (
            PromoteFromEventRequest request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService service,
            EventService events,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return Results.Json(
                    new { error = new { code = "session_unavailable", message = "No active ITMG user session." } },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            if (request.EventId == Guid.Empty)
            {
                return ValidationProblem("eventId is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            {
                return ValidationProblem("title and description are required.");
            }

            if (!await events.ExistsAsync(request.EventId, cancellationToken))
            {
                return Results.Json(
                    new { error = new { code = "not_found", message = "Event was not found." } },
                    statusCode: StatusCodes.Status404NotFound);
            }

            TicketPriority priority = TicketPriority.Medium;
            if (!string.IsNullOrWhiteSpace(request.Priority)
                && !Enum.TryParse(request.Priority, ignoreCase: true, out priority))
            {
                return ValidationProblem("Invalid priority.");
            }

            try
            {
                TicketEntity created = await service.PromoteFromEventAsync(
                    request.EventId,
                    request.Title,
                    request.Description,
                    session.Id,
                    priority,
                    request.ConfigurationItemId,
                    cancellationToken);

                await events.MarkPromotedAsync(request.EventId, created.Id, cancellationToken);

                bool includeSecurity = session.Permissions.Any(item =>
                    string.Equals(item, TicketEndpoints.IncidentsSecurity, StringComparison.OrdinalIgnoreCase));
                TicketDto? dto = await service.GetAsync(created.Id, includeSecurity, cancellationToken);
                return Results.Created($"/api/v1/tickets/{created.Id}", dto);
            }
            catch (ArgumentException ex)
            {
                return ValidationProblem(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return FromDomainError(ex);
            }
        }).RequirePermission(TicketEndpoints.TicketsManage);

        return endpoints;
    }

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

        return Results.Json(
            new { error = new { code = "invalid_operation", message } },
            statusCode: StatusCodes.Status400BadRequest);
    }
}

public sealed record PromoteFromEventRequest(
    Guid EventId,
    string Title,
    string Description,
    string? Priority,
    Guid? ConfigurationItemId);
