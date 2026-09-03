using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Platform.Notifications;

namespace Qec.Itmg.Host.Notifications;

/// <summary>
/// Current-user notification APIs. Ownership is enforced by resolving the ITMG user session
/// and scoping every query/mutation to that user id.
/// </summary>
public static class MeNotificationEndpoints
{
    public static IEndpointRouteBuilder MapMeNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/me/notifications", async (
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            INotificationService notifications,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await ResolveSessionAsync(principal, currentUser, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            IReadOnlyList<NotificationDto> items =
                await notifications.ListForUserAsync(session.Id, take: 20, cancellationToken);
            return Results.Ok(items);
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/me/notifications/unread-count", async (
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            INotificationService notifications,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await ResolveSessionAsync(principal, currentUser, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            int count = await notifications.GetUnreadCountAsync(session.Id, cancellationToken);
            return Results.Ok(new { count });
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/me/notifications/{id:guid}/read", async (
            Guid id,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            INotificationService notifications,
            CancellationToken cancellationToken) =>
        {
            CurrentUserDto? session = await ResolveSessionAsync(principal, currentUser, cancellationToken);
            if (session is null)
            {
                return SessionUnavailable();
            }

            NotificationDto? updated = await notifications.MarkReadAsync(session.Id, id, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
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
}
