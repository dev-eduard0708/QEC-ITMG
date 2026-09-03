using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Qec.Itmg.Identity.CurrentUser;

public static class CurrentUserEndpoints
{
    public static IServiceCollection AddCurrentUserServices(this IServiceCollection services)
    {
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        return services;
    }

    public static IEndpointRouteBuilder MapCurrentUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/me", async (
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            CancellationToken cancellationToken) =>
        {
            if (principal.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, cancellationToken);
            if (session is null)
            {
                return Results.Json(
                    new { error = new { code = "session_unavailable", message = "No active ITMG user session." } },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return Results.Ok(session);
        }).RequireAuthorization();

        return endpoints;
    }
}
