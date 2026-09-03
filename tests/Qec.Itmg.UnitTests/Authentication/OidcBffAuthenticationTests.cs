using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Qec.Itmg.Identity.Authentication;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.ServiceDesk.Persistence;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Authentication;

[Collection(WebApplicationFactoryCollection.Name)]
public sealed class OidcBffAuthenticationTests
{
    [Fact]
    public async Task Host_Starts_WhenOidcDisabled()
    {
        await using AuthWebApplicationFactory factory = new(oidcEnabled: false);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WhenOidcDisabled_Returns503()
    {
        await using AuthWebApplicationFactory factory = new(oidcEnabled: false);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        HttpResponseMessage response = await client.GetAsync("/auth/login");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task DevLogin_WhenNotDevelopment_IsNotMapped()
    {
        await using AuthWebApplicationFactory factory = new(oidcEnabled: false);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage admin = await client.PostAsync("/auth/dev-login/admin", content: null);
        HttpResponseMessage employee = await client.PostAsync("/auth/dev-login/employee", content: null);

        Assert.Equal(HttpStatusCode.NotFound, admin.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, employee.StatusCode);
    }

    [Fact]
    public async Task Login_WhenOidcEnabled_ChallengesOidc()
    {
        await using AuthWebApplicationFactory factory = new(oidcEnabled: true);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        HttpResponseMessage response = await client.GetAsync("/auth/login?returnUrl=/employee");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains(
            "idp.example.test/authorize",
            response.Headers.Location!.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CookieScheme_IsConfigured_WithHttpOnlySameSiteSliding()
    {
        using AuthWebApplicationFactory factory = new(oidcEnabled: false);
        IOptionsMonitor<CookieAuthenticationOptions> cookieOptions =
            factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();

        CookieAuthenticationOptions options = cookieOptions.Get(IdentityAuthenticationExtensions.CookieScheme);

        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, options.Cookie.SameSite);
        Assert.True(options.SlidingExpiration);
        Assert.Equal(TimeSpan.FromHours(8), options.ExpireTimeSpan);
        Assert.Equal("QecItmg.Auth", options.Cookie.Name);
    }

    [Fact]
    public async Task Logout_ClearsCookieSession()
    {
        await using AuthWebApplicationFactory factory = new(oidcEnabled: false);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        HttpResponseMessage signIn = await client.GetAsync("/__test__/signin");
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        HttpResponseMessage before = await client.GetAsync("/__test__/auth-state");
        Assert.Equal("authenticated", await before.Content.ReadAsStringAsync());

        HttpResponseMessage logout = await client.PostAsync("/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        HttpResponseMessage after = await client.GetAsync("/__test__/auth-state");
        Assert.Equal("anonymous", await after.Content.ReadAsStringAsync());
    }

    [Fact]
    public void OidcPrincipalMapper_DropsIdpRolesAndGroups()
    {
        ClaimsIdentity inbound = new("oidc");
        inbound.AddClaim(new Claim("sub", "google-sub-1"));
        inbound.AddClaim(new Claim("email", "user@qehc.edu.sa"));
        inbound.AddClaim(new Claim("email_verified", "true"));
        inbound.AddClaim(new Claim("name", "User Example"));
        inbound.AddClaim(new Claim(ClaimTypes.Role, "Domain Admins"));
        inbound.AddClaim(new Claim("roles", "IT-Admins"));
        inbound.AddClaim(new Claim("groups", "group-guid"));

        ClaimsPrincipal mapped = OidcPrincipalMapper.MapAuthenticatedPrincipal(new ClaimsPrincipal(inbound));

        Assert.Equal("google-sub-1", mapped.FindFirst(OidcPrincipalMapper.ExternalIdClaimType)?.Value);
        Assert.Equal("user@qehc.edu.sa", mapped.FindFirst(OidcPrincipalMapper.UpnClaimType)?.Value);
        Assert.Equal("User Example", mapped.FindFirst(ClaimTypes.Name)?.Value);
        Assert.False(OidcPrincipalMapper.ContainsAuthorizationRoleClaims(mapped));
        Assert.DoesNotContain(mapped.Claims, claim => claim.Type is ClaimTypes.Role or "roles" or "groups");
        Assert.Empty(mapped.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public void OidcEnabled_WithoutRequiredConfig_FailsClearly()
    {
        OidcAuthenticationOptions options = new()
        {
            Enabled = true,
            Authority = string.Empty,
            ClientId = "client",
            ClientSecret = string.Empty,
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.EnsureValidWhenEnabled);
        Assert.Contains("Authority", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ClientSecret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OidcScheme_IsRegistered_WhenEnabled()
    {
        await using AuthWebApplicationFactory factory = new(oidcEnabled: true);
        IAuthenticationSchemeProvider schemes =
            factory.Services.GetRequiredService<IAuthenticationSchemeProvider>();

        Assert.NotNull(await schemes.GetSchemeAsync(IdentityAuthenticationExtensions.OidcScheme));

        OpenIdConnectOptions options = factory.Services
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(IdentityAuthenticationExtensions.OidcScheme);

        Assert.Equal(OpenIdConnectResponseType.Code, options.ResponseType);
        Assert.True(options.UsePkce);
        Assert.False(options.SaveTokens);
        Assert.Equal("qec_no_idp_roles", options.TokenValidationParameters.RoleClaimType);
    }
}

internal sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly bool _oidcEnabled;
    private readonly string _databaseName = Guid.NewGuid().ToString("N");

    public AuthWebApplicationFactory(bool oidcEnabled)
    {
        _oidcEnabled = oidcEnabled;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Authentication:Oidc:Enabled", _oidcEnabled ? "true" : "false");
        builder.UseSetting("Authentication:Oidc:Authority", "https://idp.example.test");
        builder.UseSetting("Authentication:Oidc:ClientId", "qec-itmg-test");
        builder.UseSetting("Authentication:Oidc:ClientSecret", "test-secret-not-real");
        builder.UseSetting("Authentication:Oidc:CallbackPath", "/signin-oidc");
        builder.UseSetting(
            "ConnectionStrings:QecItmg",
            "Server=(localdb)\\mssqllocaldb;Database=unused;Trusted_Connection=True;TrustServerCertificate=True");

        builder.ConfigureTestServices(services =>
        {
            RemoveDbContext<IdentityDbContext>(services);
            RemoveDbContext<OrganizationDbContext>(services);
            RemoveDbContext<PlatformDbContext>(services);
            RemoveDbContext<CmdbDbContext>(services);
            RemoveDbContext<ServiceDeskDbContext>(services);

            services.AddDbContext<IdentityDbContext>(options =>
                options.UseInMemoryDatabase($"auth-identity-{_databaseName}"));
            services.AddDbContext<OrganizationDbContext>(options =>
                options.UseInMemoryDatabase($"auth-organization-{_databaseName}"));
            services.AddDbContext<PlatformDbContext>(options =>
                options.UseInMemoryDatabase($"auth-platform-{_databaseName}"));
            services.AddDbContext<CmdbDbContext>(options =>
                options.UseInMemoryDatabase($"auth-cmdb-{_databaseName}"));
            services.AddDbContext<ServiceDeskDbContext>(options =>
                options.UseInMemoryDatabase($"auth-sd-{_databaseName}"));

            if (!_oidcEnabled)
            {
                return;
            }

            services.PostConfigure<OpenIdConnectOptions>(IdentityAuthenticationExtensions.OidcScheme, options =>
            {
                options.RequireHttpsMetadata = false;
                OpenIdConnectConfiguration configuration = new()
                {
                    Issuer = "https://idp.example.test",
                    AuthorizationEndpoint = "https://idp.example.test/authorize",
                    TokenEndpoint = "https://idp.example.test/token",
                    EndSessionEndpoint = "https://idp.example.test/logout",
                    JwksUri = "https://idp.example.test/jwks",
                };
                options.Configuration = configuration;
                options.ConfigurationManager =
                    new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        });
    }

    private static void RemoveDbContext<TContext>(IServiceCollection services)
        where TContext : DbContext
    {
        services.RemoveAll(typeof(DbContextOptions<TContext>));
        services.RemoveAll(typeof(IDbContextOptionsConfiguration<TContext>));
        services.RemoveAll(typeof(TContext));
    }
}
