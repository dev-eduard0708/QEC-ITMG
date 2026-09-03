using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Qec.Itmg.Identity.Admin;
using Qec.Itmg.Identity.Audit;
using Qec.Itmg.Identity.Authorization;

namespace Qec.Itmg.Identity.Authentication;

public static class IdentityAuthenticationExtensions
{
    public const string CookieScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string OidcScheme = OpenIdConnectDefaults.AuthenticationScheme;

    public static IServiceCollection AddIdentityAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        OidcAuthenticationOptions oidcOptions = configuration
            .GetSection(OidcAuthenticationOptions.SectionName)
            .Get<OidcAuthenticationOptions>()
            ?? new OidcAuthenticationOptions();

        oidcOptions.EnsureValidWhenEnabled();
        services.AddSingleton(Options.Create(oidcOptions));

        AuthenticationBuilder authentication = services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieScheme;
            options.DefaultAuthenticateScheme = CookieScheme;
            options.DefaultChallengeScheme = oidcOptions.Enabled ? OidcScheme : CookieScheme;
            options.DefaultSignOutScheme = CookieScheme;
        });

        authentication.AddCookie(CookieScheme, options =>
        {
            options.Cookie.Name = "QecItmg.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = environment.IsDevelopment() || environment.IsEnvironment("Testing")
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = oidcOptions.SessionLifetime;
            options.Events.OnSigningIn = async context =>
            {
                // Ensure cookie principal never carries IdP role claims for authorization.
                if (context.Principal is not null
                    && OidcPrincipalMapper.ContainsAuthorizationRoleClaims(context.Principal))
                {
                    OidcAuthenticationOptions optionsSnapshot = context.HttpContext.RequestServices
                        .GetRequiredService<IOptions<OidcAuthenticationOptions>>()
                        .Value;
                    context.Principal = OidcPrincipalMapper.MapAuthenticatedPrincipal(
                        context.Principal,
                        optionsSnapshot.AllowedDomains);
                }

                await SecurityAuditHooks.LogLoginSuccessAsync(context.HttpContext);
            };
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
        });

        if (oidcOptions.Enabled)
        {
            authentication.AddOpenIdConnect(OidcScheme, options =>
            {
                options.Authority = oidcOptions.Authority.TrimEnd('/');
                options.ClientId = oidcOptions.ClientId;
                options.ClientSecret = oidcOptions.ClientSecret;
                options.CallbackPath = oidcOptions.CallbackPath;
                options.SignedOutCallbackPath = oidcOptions.SignedOutCallbackPath;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;
                options.SaveTokens = false;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.MapInboundClaims = false;
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.TokenValidationParameters.NameClaimType = ClaimTypes.Name;
                // Deliberately not RoleClaimType = "roles" — ITMG authorization is SQL RBAC.
                options.TokenValidationParameters.RoleClaimType = "qec_no_idp_roles";
                options.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (context.Principal is null)
                        {
                            context.Fail("OIDC token validated without a principal.");
                            return Task.CompletedTask;
                        }

                        try
                        {
                            context.Principal = OidcPrincipalMapper.MapAuthenticatedPrincipal(
                                context.Principal,
                                oidcOptions.AllowedDomains);
                        }
                        catch (InvalidOperationException exception)
                        {
                            context.Fail(exception.Message);
                        }

                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = async context =>
                    {
                        await SecurityAuditHooks.LogLoginFailureAsync(
                            context.HttpContext,
                            context.Exception?.Message);
                    },
                    OnRemoteFailure = async context =>
                    {
                        await SecurityAuditHooks.LogLoginFailureAsync(
                            context.HttpContext,
                            context.Failure?.Message);
                        context.Response.Redirect("/?authError=remote");
                        context.HandleResponse();
                    },
                };
            });
        }

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        services.AddScoped<IUserPermissionEvaluator, SqlUserPermissionEvaluator>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization();
        services.AddIdentityAdminServices();
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/auth/login", (HttpContext httpContext, IOptions<OidcAuthenticationOptions> options) =>
        {
            if (!options.Value.Enabled)
            {
                return Results.Json(
                    new { error = "OIDC authentication is disabled in this environment." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            string? returnUrl = httpContext.Request.Query["returnUrl"].FirstOrDefault();
            AuthenticationProperties properties = new()
            {
                RedirectUri = LocalReturnUrl.Sanitize(returnUrl),
            };

            return Results.Challenge(properties, [OidcScheme]);
        });

        endpoints.MapPost("/auth/logout", async (
            HttpContext httpContext,
            IOptions<OidcAuthenticationOptions> options) =>
        {
            await SecurityAuditHooks.LogLogoutAsync(httpContext);

            if (options.Value.Enabled)
            {
                AuthenticationProperties properties = new()
                {
                    RedirectUri = "/",
                };

                return Results.SignOut(properties, [CookieScheme, OidcScheme]);
            }

            await httpContext.SignOutAsync(CookieScheme);
            return Results.Ok(new { signedOut = true });
        });

        return endpoints;
    }
}
