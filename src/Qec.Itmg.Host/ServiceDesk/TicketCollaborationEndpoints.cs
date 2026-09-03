using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Platform.Attachments;
using Qec.Itmg.Platform.Comments;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.ServiceDesk.Services;

namespace Qec.Itmg.Host.ServiceDesk;

public static class TicketCollaborationEndpoints
{
    public const string TicketResourceType = "Ticket";

    public static IEndpointRouteBuilder MapTicketCollaborationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder readGroup = endpoints.MapGroup("/api/v1/tickets/{ticketId:guid}")
            .RequirePermission(TicketEndpoints.TicketsRead);

        readGroup.MapGet("/comments", async (
            Guid ticketId,
            TicketService tickets,
            ICommentService comments,
            CancellationToken cancellationToken) =>
        {
            if (await tickets.GetAsync(ticketId, cancellationToken) is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(await comments.ListAsync(TicketResourceType, ticketId, cancellationToken: cancellationToken));
        });

        readGroup.MapGet("/attachments", async (
            Guid ticketId,
            TicketService tickets,
            IAttachmentStorageService attachments,
            CancellationToken cancellationToken) =>
        {
            if (await tickets.GetAsync(ticketId, cancellationToken) is null)
            {
                return Results.NotFound();
            }

            IReadOnlyList<AttachmentMetadata> items =
                await attachments.ListByResourceAsync(TicketResourceType, ticketId, cancellationToken);
            return Results.Ok(items.Select(MapAttachment).ToList());
        });

        readGroup.MapGet("/attachments/{attachmentId:guid}/content", async (
            Guid ticketId,
            Guid attachmentId,
            TicketService tickets,
            IAttachmentStorageService attachments,
            CancellationToken cancellationToken) =>
        {
            if (await tickets.GetAsync(ticketId, cancellationToken) is null)
            {
                return Results.NotFound();
            }

            AttachmentMetadata? metadata = await attachments.GetMetadataAsync(attachmentId, cancellationToken);
            if (metadata is null
                || !string.Equals(metadata.ResourceType, TicketResourceType, StringComparison.Ordinal)
                || metadata.ResourceId != ticketId)
            {
                return Results.NotFound();
            }

            Stream stream = await attachments.OpenReadAsync(attachmentId, cancellationToken);
            return Results.File(stream, metadata.ContentType, metadata.OriginalFileName);
        });

        readGroup.MapGet("/timeline", async (
            Guid ticketId,
            TicketService tickets,
            ICommentService comments,
            IAttachmentStorageService attachments,
            CancellationToken cancellationToken) =>
        {
            TicketDto? ticket = await tickets.GetAsync(ticketId, cancellationToken);
            if (ticket is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(await BuildTimelineAsync(
                ticket,
                includeInternalComments: true,
                tickets,
                comments,
                attachments,
                cancellationToken));
        });

        RouteGroupBuilder manageGroup = endpoints.MapGroup("/api/v1/tickets/{ticketId:guid}")
            .RequirePermission(TicketEndpoints.TicketsManage);

        manageGroup.MapPost("/comments", async (
            Guid ticketId,
            AddTicketCommentRequest request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService tickets,
            ICommentService comments,
            TicketNotificationService notifications,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            TicketDto? ticket = await tickets.GetAsync(ticketId, cancellationToken);
            if (ticket is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(request.Body))
            {
                return ValidationProblem("Comment body is required.");
            }

            CommentVisibility visibility = CommentVisibility.Internal;
            if (!string.IsNullOrWhiteSpace(request.Visibility)
                && !Enum.TryParse(request.Visibility, ignoreCase: true, out visibility))
            {
                return ValidationProblem("Invalid visibility.");
            }

            CommentTimelineItem comment = await comments.AddAsync(
                TicketResourceType,
                ticketId,
                session.Id,
                request.Body,
                visibility,
                cancellationToken);

            if (visibility == CommentVisibility.EmployeeVisible)
            {
                await notifications.NotifyEmployeeVisibleCommentAsync(
                    ticket,
                    session.Id,
                    request.Body,
                    cancellationToken);
            }

            return Results.Created($"/api/v1/tickets/{ticketId}/comments", comment);
        });

        manageGroup.MapPost("/attachments", async (
            Guid ticketId,
            HttpRequest httpRequest,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService tickets,
            IAttachmentStorageService attachments,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            if (await tickets.GetAsync(ticketId, cancellationToken) is null)
            {
                return Results.NotFound();
            }

            IFormFile? file = httpRequest.Form.Files.GetFile("file") ?? httpRequest.Form.Files.FirstOrDefault();
            if (file is null || file.Length <= 0)
            {
                return ValidationProblem("A non-empty file is required.");
            }

            await using Stream stream = file.OpenReadStream();
            AttachmentMetadata metadata = await attachments.StoreAsync(
                stream,
                file.FileName,
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                session.Id,
                TicketResourceType,
                ticketId,
                cancellationToken);

            return Results.Created(
                $"/api/v1/tickets/{ticketId}/attachments/{metadata.Id}",
                MapAttachment(metadata));
        }).DisableAntiforgery();

        MapMeCollaboration(endpoints);
        return endpoints;
    }

    private static void MapMeCollaboration(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/me/tickets/{ticketId:guid}/comments", async (
            Guid ticketId,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService tickets,
            ICommentService comments,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await ResolveSessionAsync(principal, currentUser, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            if (await tickets.GetForRequesterAsync(ticketId, session.Id, cancellationToken) is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(await comments.ListAsync(
                TicketResourceType,
                ticketId,
                CommentVisibility.EmployeeVisible,
                cancellationToken));
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/me/tickets/{ticketId:guid}/comments", async (
            Guid ticketId,
            AddTicketCommentRequest request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService tickets,
            ICommentService comments,
            TicketNotificationService notifications,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await ResolveSessionAsync(principal, currentUser, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            TicketDto? ticket = await tickets.GetForRequesterAsync(ticketId, session.Id, cancellationToken);
            if (ticket is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(request.Body))
            {
                return ValidationProblem("Comment body is required.");
            }

            CommentTimelineItem comment = await comments.AddAsync(
                TicketResourceType,
                ticketId,
                session.Id,
                request.Body,
                CommentVisibility.EmployeeVisible,
                cancellationToken);

            await notifications.NotifyEmployeeVisibleCommentAsync(
                ticket,
                session.Id,
                request.Body,
                cancellationToken);

            return Results.Created($"/api/v1/me/tickets/{ticketId}/comments", comment);
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/me/tickets/{ticketId:guid}/attachments", async (
            Guid ticketId,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService tickets,
            IAttachmentStorageService attachments,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await ResolveSessionAsync(principal, currentUser, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            if (await tickets.GetForRequesterAsync(ticketId, session.Id, cancellationToken) is null)
            {
                return Results.NotFound();
            }

            IReadOnlyList<AttachmentMetadata> items =
                await attachments.ListByResourceAsync(TicketResourceType, ticketId, cancellationToken);
            return Results.Ok(items.Select(MapAttachment).ToList());
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/me/tickets/{ticketId:guid}/attachments", async (
            Guid ticketId,
            HttpRequest httpRequest,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService tickets,
            IAttachmentStorageService attachments,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await ResolveSessionAsync(principal, currentUser, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            if (await tickets.GetForRequesterAsync(ticketId, session.Id, cancellationToken) is null)
            {
                return Results.NotFound();
            }

            IFormFile? file = httpRequest.Form.Files.GetFile("file") ?? httpRequest.Form.Files.FirstOrDefault();
            if (file is null || file.Length <= 0)
            {
                return ValidationProblem("A non-empty file is required.");
            }

            await using Stream stream = file.OpenReadStream();
            AttachmentMetadata metadata = await attachments.StoreAsync(
                stream,
                file.FileName,
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                session.Id,
                TicketResourceType,
                ticketId,
                cancellationToken);

            return Results.Created(
                $"/api/v1/me/tickets/{ticketId}/attachments/{metadata.Id}",
                MapAttachment(metadata));
        }).RequireAuthorization().DisableAntiforgery();

        endpoints.MapGet("/api/v1/me/tickets/{ticketId:guid}/attachments/{attachmentId:guid}/content", async (
            Guid ticketId,
            Guid attachmentId,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService tickets,
            IAttachmentStorageService attachments,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await ResolveSessionAsync(principal, currentUser, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            if (await tickets.GetForRequesterAsync(ticketId, session.Id, cancellationToken) is null)
            {
                return Results.NotFound();
            }

            AttachmentMetadata? metadata = await attachments.GetMetadataAsync(attachmentId, cancellationToken);
            if (metadata is null
                || !string.Equals(metadata.ResourceType, TicketResourceType, StringComparison.Ordinal)
                || metadata.ResourceId != ticketId)
            {
                return Results.NotFound();
            }

            Stream stream = await attachments.OpenReadAsync(attachmentId, cancellationToken);
            return Results.File(stream, metadata.ContentType, metadata.OriginalFileName);
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/me/tickets/{ticketId:guid}/timeline", async (
            Guid ticketId,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            TicketService tickets,
            ICommentService comments,
            IAttachmentStorageService attachments,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await ResolveSessionAsync(principal, currentUser, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            TicketDto? ticket = await tickets.GetForRequesterAsync(ticketId, session.Id, cancellationToken);
            if (ticket is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(await BuildTimelineAsync(
                ticket,
                includeInternalComments: false,
                tickets,
                comments,
                attachments,
                cancellationToken));
        }).RequireAuthorization();
    }

    internal static async Task<IReadOnlyList<TicketTimelineItemDto>> BuildTimelineAsync(
        TicketDto ticket,
        bool includeInternalComments,
        TicketService tickets,
        ICommentService comments,
        IAttachmentStorageService attachments,
        CancellationToken cancellationToken)
    {
        List<TicketTimelineItemDto> items =
        [
            new(
                $"created-{ticket.Id}",
                "created",
                ticket.CreatedAtUtc,
                "Ticket created",
                $"{ticket.TicketNumber} · {ticket.Type}",
                ticket.RequesterUserId.ToString("D"),
                ticket.Status),
        ];

        foreach (TicketAssignmentHistoryDto row in await tickets.ListAssignmentHistoryAsync(ticket.Id, cancellationToken))
        {
            items.Add(new(
                $"assign-{row.Id}",
                "assignment",
                row.AssignedAtUtc,
                "Assignment updated",
                row.Notes,
                row.AssignedByUserId.ToString("D"),
                row.AssignedUserId?.ToString("D")));
        }

        foreach (TicketStatusHistoryDto row in await tickets.ListStatusHistoryAsync(ticket.Id, cancellationToken))
        {
            items.Add(new(
                $"status-{row.Id}",
                "status",
                row.ChangedAtUtc,
                $"Status: {row.FromStatus} → {row.ToStatus}",
                null,
                row.ChangedByUserId.ToString("D"),
                row.ToStatus));
        }

        CommentVisibility? filter = includeInternalComments ? null : CommentVisibility.EmployeeVisible;
        foreach (CommentTimelineItem comment in await comments.ListAsync(
                     TicketResourceType,
                     ticket.Id,
                     filter,
                     cancellationToken))
        {
            items.Add(new(
                $"comment-{comment.Id}",
                "comment",
                comment.CreatedAtUtc,
                comment.Visibility == nameof(CommentVisibility.Internal) ? "Internal comment" : "Comment",
                comment.Body,
                comment.AuthorUserId.ToString("D"),
                comment.Visibility));
        }

        foreach (AttachmentMetadata attachment in await attachments.ListByResourceAsync(
                     TicketResourceType,
                     ticket.Id,
                     cancellationToken))
        {
            items.Add(new(
                $"attachment-{attachment.Id}",
                "attachment",
                attachment.UploadedAtUtc,
                "Attachment uploaded",
                $"{attachment.OriginalFileName} ({attachment.SizeBytes} bytes)",
                attachment.UploadedByUserId.ToString("D"),
                attachment.ScanStatus.ToString()));
        }

        return items
            .OrderBy(item => item.Timestamp)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static TicketAttachmentDto MapAttachment(AttachmentMetadata metadata) =>
        new(
            metadata.Id,
            metadata.OriginalFileName,
            metadata.ContentType,
            metadata.SizeBytes,
            metadata.ScanStatus.ToString(),
            metadata.UploadedByUserId,
            metadata.UploadedAtUtc);

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
}

public sealed record AddTicketCommentRequest(string Body, string? Visibility);

public sealed record TicketAttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string ScanStatus,
    Guid UploadedByUserId,
    DateTimeOffset UploadedAtUtc);

public sealed record TicketTimelineItemDto(
    string Id,
    string Type,
    DateTimeOffset Timestamp,
    string Title,
    string? Description,
    string? Actor,
    string? Status);
