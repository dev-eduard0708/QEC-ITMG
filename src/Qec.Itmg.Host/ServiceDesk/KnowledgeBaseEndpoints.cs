using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.ServiceDesk.Services;

namespace Qec.Itmg.Host.ServiceDesk;

public static class KnowledgeBaseEndpoints
{
    public const string KbRead = "kb.read";
    public const string KbManage = "kb.manage";

    public static IEndpointRouteBuilder MapKnowledgeBaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Admin routes first so "admin" is not captured by {slug}.
        RouteGroupBuilder adminRead = endpoints.MapGroup("/api/v1/kb/admin")
            .RequirePermission(KbRead);

        adminRead.MapGet(string.Empty, async (
            string? status,
            string? search,
            KnowledgeArticleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAdminAsync(status, search, cancellationToken)));

        adminRead.MapGet("/{id:guid}", async (
            Guid id,
            KnowledgeArticleService service,
            CancellationToken cancellationToken) =>
        {
            KnowledgeArticleDto? article = await service.GetAdminAsync(id, cancellationToken);
            return article is null ? Results.NotFound() : Results.Ok(article);
        });

        RouteGroupBuilder adminManage = endpoints.MapGroup("/api/v1/kb/admin")
            .RequirePermission(KbManage);

        adminManage.MapPost(string.Empty, async (
            UpsertKnowledgeArticleRequest request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            KnowledgeArticleService service,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            if (string.IsNullOrWhiteSpace(request.Title)
                || string.IsNullOrWhiteSpace(request.Slug)
                || string.IsNullOrWhiteSpace(request.Body))
            {
                return ValidationProblem("title, slug, and body are required.");
            }

            try
            {
                var created = await service.CreateAsync(
                    request.Title,
                    request.Slug,
                    request.Body,
                    session.Id,
                    request.Summary,
                    cancellationToken);
                return Results.Created(
                    $"/api/v1/kb/admin/{created.Id}",
                    await service.GetAdminAsync(created.Id, cancellationToken));
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

        adminManage.MapPut("/{id:guid}", async (
            Guid id,
            UpsertKnowledgeArticleRequest request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            KnowledgeArticleService service,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            if (string.IsNullOrWhiteSpace(request.Title)
                || string.IsNullOrWhiteSpace(request.Slug)
                || string.IsNullOrWhiteSpace(request.Body))
            {
                return ValidationProblem("title, slug, and body are required.");
            }

            try
            {
                await service.UpdateAsync(
                    id,
                    request.Title,
                    request.Slug,
                    request.Body,
                    session.Id,
                    request.Summary,
                    cancellationToken);
                return Results.Ok(await service.GetAdminAsync(id, cancellationToken));
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

        adminManage.MapPost("/{id:guid}/publish", async (
            Guid id,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            KnowledgeArticleService service,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            try
            {
                await service.PublishAsync(id, session.Id, cancellationToken);
                return Results.Ok(await service.GetAdminAsync(id, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return FromDomainError(ex);
            }
        });

        adminManage.MapPost("/{id:guid}/archive", async (
            Guid id,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            KnowledgeArticleService service,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            try
            {
                await service.ArchiveAsync(id, session.Id, cancellationToken);
                return Results.Ok(await service.GetAdminAsync(id, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return FromDomainError(ex);
            }
        });

        endpoints.MapGet("/api/v1/kb", async (
            string? search,
            KnowledgeArticleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListPublishedAsync(search, cancellationToken)))
            .RequireAuthorization();

        endpoints.MapGet("/api/v1/kb/{slug}", async (
            string slug,
            KnowledgeArticleService service,
            CancellationToken cancellationToken) =>
        {
            if (string.Equals(slug, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return Results.NotFound();
            }

            try
            {
                KnowledgeArticleDto? article = await service.GetPublishedBySlugAsync(slug, cancellationToken);
                return article is null ? Results.NotFound() : Results.Ok(article);
            }
            catch (ArgumentException ex)
            {
                return ValidationProblem(ex.Message);
            }
        }).RequireAuthorization();

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
        if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(
                new { error = new { code = "not_found", message = ex.Message } },
                statusCode: StatusCodes.Status404NotFound);
        }

        if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(
                new { error = new { code = "conflict", message = ex.Message } },
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Json(
            new { error = new { code = "invalid_operation", message = ex.Message } },
            statusCode: StatusCodes.Status400BadRequest);
    }
}

public sealed record UpsertKnowledgeArticleRequest(
    string Title,
    string Slug,
    string Body,
    string? Summary);
