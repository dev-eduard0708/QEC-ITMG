using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.Identity.Authorization;

namespace Qec.Itmg.Identity.Admin;

public static class IdentityAdminEndpoints
{
    public const string UsersPermission = "admin.users";
    public const string RolesPermission = "admin.roles";

    public static IServiceCollection AddIdentityAdminServices(this IServiceCollection services)
    {
        services.AddScoped<AdminUsersService>();
        services.AddScoped<AdminRolesService>();
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder users = endpoints.MapGroup("/api/v1/admin/users")
            .RequirePermission(UsersPermission);

        users.MapGet(string.Empty, async (string? search, AdminUsersService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(search, cancellationToken)));

        users.MapPost(string.Empty, async (CreateAdminUserRequest request, AdminUsersService service, CancellationToken cancellationToken) =>
            await service.CreateAsync(request, cancellationToken));

        users.MapPut("/{id:guid}", async (
            Guid id,
            UpdateAdminUserRequest request,
            AdminUsersService service,
            CancellationToken cancellationToken) =>
            await service.UpdateAsync(id, request, cancellationToken));

        users.MapPut("/{id:guid}/roles", async (
            Guid id,
            ReplaceUserRolesRequest request,
            AdminUsersService service,
            CancellationToken cancellationToken) =>
            await service.ReplaceRolesAsync(id, request, cancellationToken));

        RouteGroupBuilder roles = endpoints.MapGroup("/api/v1/admin/roles")
            .RequirePermission(RolesPermission);

        roles.MapGet(string.Empty, async (AdminRolesService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken)));

        roles.MapGet("/{id:guid}", async (Guid id, AdminRolesService service, CancellationToken cancellationToken) =>
            await service.GetAsync(id, cancellationToken));

        roles.MapPost(string.Empty, async (CreateAdminRoleRequest request, AdminRolesService service, CancellationToken cancellationToken) =>
            await service.CreateAsync(request, cancellationToken));

        roles.MapPut("/{id:guid}", async (
            Guid id,
            UpdateAdminRoleRequest request,
            AdminRolesService service,
            CancellationToken cancellationToken) =>
            await service.UpdateAsync(id, request, cancellationToken));

        roles.MapPut("/{id:guid}/permissions", async (
            Guid id,
            ReplaceRolePermissionsRequest request,
            AdminRolesService service,
            CancellationToken cancellationToken) =>
            await service.ReplacePermissionsAsync(id, request, cancellationToken));

        endpoints.MapGet("/api/v1/admin/permissions", async (
                AdminRolesService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.ListPermissionsAsync(cancellationToken)))
            .RequirePermission(RolesPermission);

        return endpoints;
    }
}
