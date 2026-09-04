using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Operations.Domain;
using Qec.Itmg.Operations.Services;
using Qec.Itmg.ServiceDesk.Domain;
using Qec.Itmg.ServiceDesk.Services;

namespace Qec.Itmg.Host.Operations;

public static class EventEndpoints
{
    public const string EventRead = "event.read";
    public const string EventAcknowledge = "event.acknowledge";
    public const string EventPromote = "event.promote";
    public const string EventAdmin = "event.admin";

    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/events").RequirePermission(EventRead);

        read.MapGet(string.Empty, async (
            int? page, int? pageSize, string? search, string? status, string? severity, string? source,
            EventService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(
                page ?? 1, pageSize ?? 25, search,
                ParseEnum<EventStatus>(status), ParseEnum<EventSeverity>(severity), source, ct)));

        read.MapGet("/{id:guid}", async (Guid id, EventService service, CancellationToken ct) =>
        {
            EventDto? item = await service.GetAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        endpoints.MapPost("/api/v1/events/ingest", async (
            IngestEventRequest request,
            EventService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Source) || string.IsNullOrWhiteSpace(request.SourceEventKey))
                return ValidationProblem("source and sourceEventKey are required.");
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Summary))
                return ValidationProblem("title and summary are required.");
            if (!Enum.TryParse(request.Severity, true, out EventSeverity severity))
                return ValidationProblem("A valid severity is required.");
            try
            {
                IngestResult result = await service.IngestAsync(
                    request.Source, request.SourceEventKey, severity, request.Title, request.Summary,
                    request.ConfigurationItemId, ct);
                return result.Created
                    ? Results.Created($"/api/v1/events/{result.Event.Id}", result)
                    : Results.Ok(result);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return ex is ArgumentException a ? ValidationProblem(a.Message) : FromDomainError((InvalidOperationException)ex);
            }
        }).RequirePermission(EventAdmin);

        endpoints.MapPost("/api/v1/events/{id:guid}/acknowledge", async (
            Guid id,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            EventService service,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                return Results.Ok(await service.AcknowledgeAsync(id, session.Id, ct));
            }
            catch (InvalidOperationException ex) { return FromDomainError(ex); }
        }).RequirePermission(EventAcknowledge);

        endpoints.MapPost("/api/v1/events/{id:guid}/promote", async (
            Guid id,
            PromoteEventRequest? request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            EventService events,
            TicketService tickets,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();

            EventDto? evt = await events.GetAsync(id, ct);
            if (evt is null) return Results.NotFound();
            if (evt.Status == nameof(EventStatus.Closed))
                return ValidationProblem("Closed events cannot be promoted.");

            string title = string.IsNullOrWhiteSpace(request?.Title) ? evt.Title : request!.Title!;
            string description = string.IsNullOrWhiteSpace(request?.Description) ? evt.Summary : request!.Description!;
            TicketPriority priority = MapPriority(evt.Severity);
            if (!string.IsNullOrWhiteSpace(request?.Priority)
                && Enum.TryParse(request.Priority, true, out TicketPriority parsed))
            {
                priority = parsed;
            }

            try
            {
                var ticket = await tickets.PromoteFromEventAsync(
                    id, title, description, session.Id, priority, evt.ConfigurationItemId, ct);
                EventDto updated = await events.MarkPromotedAsync(id, ticket.Id, ct);
                return Results.Ok(new PromoteEventResponse(updated, ticket.Id, ticket.TicketNumber));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return ex is ArgumentException a ? ValidationProblem(a.Message) : FromDomainError((InvalidOperationException)ex);
            }
        }).RequirePermission(EventPromote);

        endpoints.MapPost("/api/v1/events/{id:guid}/close", async (
            Guid id, EventService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.CloseAsync(id, ct)); }
            catch (InvalidOperationException ex) { return FromDomainError(ex); }
        }).RequirePermission(EventAcknowledge);

        return endpoints;
    }

    private static TicketPriority MapPriority(string severity) =>
        severity switch
        {
            nameof(EventSeverity.Emergency) or nameof(EventSeverity.Critical) => TicketPriority.Critical,
            nameof(EventSeverity.Warning) => TicketPriority.High,
            _ => TicketPriority.Medium,
        };

    private static IResult SessionUnavailable() =>
        Results.Json(new { error = new { code = "session_unavailable", message = "No active ITMG user session." } },
            statusCode: StatusCodes.Status403Forbidden);

    private static IResult ValidationProblem(string message) =>
        Results.Json(new { error = new { code = "validation_error", message } }, statusCode: StatusCodes.Status400BadRequest);

    private static IResult FromDomainError(InvalidOperationException ex)
    {
        string message = ex.Message;
        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return Results.Json(new { error = new { code = "not_found", message } }, statusCode: StatusCodes.Status404NotFound);
        return Results.Json(new { error = new { code = "invalid_operation", message } }, statusCode: StatusCodes.Status400BadRequest);
    }

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null : Enum.TryParse(value, true, out TEnum parsed) ? parsed : null;
}

public sealed record IngestEventRequest(
    string Source, string SourceEventKey, string Severity, string Title, string Summary, Guid? ConfigurationItemId);

public sealed record PromoteEventRequest(string? Title, string? Description, string? Priority);
public sealed record PromoteEventResponse(EventDto Event, Guid TicketId, string TicketNumber);
