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

        BreakGlassAuthenticationOptions breakGlassOptions = configuration
            .GetSection(BreakGlassAuthenticationOptions.SectionName)
            .Get<BreakGlassAuthenticationOptions>()
            ?? new BreakGlassAuthenticationOptions();
        services.Configure<BreakGlassAuthenticationOptions>(options =>
        {
            options.Enabled = breakGlassOptions.Enabled;
            options.Accounts = breakGlassOptions.Accounts;
        });
        services.AddScoped<IBreakGlassLoginService, BreakGlassLoginService>();

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

                if (BreakGlassPrincipalFactory.IsBreakGlass(context.Principal))
                {
                    // Break-glass success is audited by the break-glass endpoint with username context.
                    return;
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

        endpoints.MapPost("/auth/break-glass", async (
            BreakGlassLoginRequest request,
            HttpContext httpContext,
            IBreakGlassLoginService breakGlassLogin,
            CancellationToken cancellationToken) =>
        {
            BreakGlassLoginResult result = await breakGlassLogin.AuthenticateAsync(request, cancellationToken);
            if (!result.Succeeded)
            {
                string reason = result.FailureReason switch
                {
                    BreakGlassLoginFailureReason.Disabled => "disabled",
                    BreakGlassLoginFailureReason.UserInactiveOrMissing => "user_inactive_or_missing",
                    _ => "invalid_credentials",
                };

                await SecurityAuditHooks.LogBreakGlassLoginFailedAsync(
                    httpContext,
                    request.Username,
                    reason);

                return result.FailureReason switch
                {
                    BreakGlassLoginFailureReason.Disabled => Results.Json(
                        new { error = new { code = "break_glass_disabled", message = "Break-glass authentication is disabled." } },
                        statusCode: StatusCodes.Status503ServiceUnavailable),
                    BreakGlassLoginFailureReason.UserInactiveOrMissing => Results.Json(
                        new { error = new { code = "break_glass_user_inactive", message = "Mapped ITMG user is missing or inactive." } },
                        statusCode: StatusCodes.Status403Forbidden),
                    _ => Results.Json(
                        new { error = new { code = "break_glass_invalid_credentials", message = "Invalid break-glass credentials." } },
                        statusCode: StatusCodes.Status401Unauthorized),
                };
            }

            ClaimsPrincipal principal = BreakGlassPrincipalFactory.Create(result.User!);
            await httpContext.SignInAsync(CookieScheme, principal);
            await SecurityAuditHooks.LogBreakGlassLoginSuccessAsync(httpContext, request.Username);

            return Results.Ok(new
            {
                signedIn = true,
                authMethod = BreakGlassPrincipalFactory.AuthMethodBreakGlass,
                upn = result.User!.Upn,
            });
        });

        return endpoints;
    }
}
