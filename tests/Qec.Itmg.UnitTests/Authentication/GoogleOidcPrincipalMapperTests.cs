using System.Security.Claims;
using Qec.Itmg.Identity.Authentication;
using Xunit;

namespace Qec.Itmg.UnitTests.Authentication;

public sealed class GoogleOidcPrincipalMapperTests
{
    [Fact]
    public void Map_GoogleSubEmailName_ToAppClaims()
    {
        ClaimsPrincipal mapped = OidcPrincipalMapper.MapAuthenticatedPrincipal(CreateGooglePrincipal(
            sub: "google-sub-123",
            email: "alice@qehc.edu.sa",
            name: "Alice Example",
            emailVerified: "true"));

        Assert.Equal("google-sub-123", mapped.FindFirst(OidcPrincipalMapper.ExternalIdClaimType)?.Value);
        Assert.Equal("alice@qehc.edu.sa", mapped.FindFirst(OidcPrincipalMapper.UpnClaimType)?.Value);
        Assert.Equal("alice@qehc.edu.sa", mapped.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal("Alice Example", mapped.FindFirst(ClaimTypes.Name)?.Value);
        Assert.False(OidcPrincipalMapper.ContainsAuthorizationRoleClaims(mapped));
    }

    [Fact]
    public void Map_RejectsUnverifiedEmail()
    {
        ClaimsPrincipal inbound = CreateGooglePrincipal(
            sub: "google-sub-456",
            email: "bob@qehc.edu.sa",
            name: "Bob",
            emailVerified: "false");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => OidcPrincipalMapper.MapAuthenticatedPrincipal(inbound));

        Assert.Contains("email_verified", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Map_IgnoresGoogleGroupsAndRoles_SqlRbacRemainsSource()
    {
        ClaimsIdentity inbound = new("oidc");
        inbound.AddClaim(new Claim("sub", "google-sub-789"));
        inbound.AddClaim(new Claim("email", "carol@qehc.edu.sa"));
        inbound.AddClaim(new Claim("email_verified", "true"));
        inbound.AddClaim(new Claim("name", "Carol"));
        inbound.AddClaim(new Claim(ClaimTypes.Role, "Google Workspace Admin"));
        inbound.AddClaim(new Claim("roles", "itmg.admin"));
        inbound.AddClaim(new Claim("groups", "group-guid-does-not-grant"));

        ClaimsPrincipal mapped = OidcPrincipalMapper.MapAuthenticatedPrincipal(new ClaimsPrincipal(inbound));

        Assert.Equal("google-sub-789", mapped.FindFirst(OidcPrincipalMapper.ExternalIdClaimType)?.Value);
        Assert.DoesNotContain(mapped.Claims, claim => claim.Type is ClaimTypes.Role or "roles" or "groups");
        Assert.False(OidcPrincipalMapper.ContainsAuthorizationRoleClaims(mapped));
        Assert.Empty(mapped.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public void Map_RejectsDisallowedDomain_WhenConfigured()
    {
        ClaimsPrincipal inbound = CreateGooglePrincipal(
            sub: "google-sub-out",
            email: "outsider@gmail.com",
            name: "Outsider",
            emailVerified: "true");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => OidcPrincipalMapper.MapAuthenticatedPrincipal(inbound, ["qehc.edu.sa"]));

        Assert.Contains("AllowedDomains", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Map_DoesNotUseEntraOidOrPreferredUsername()
    {
        ClaimsIdentity inbound = new("oidc");
        inbound.AddClaim(new Claim("oid", "entra-object-id"));
        inbound.AddClaim(new Claim("preferred_username", "entra@qehc.edu.sa"));
        inbound.AddClaim(new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", "ms-oid"));
        inbound.AddClaim(new Claim("sub", "google-sub-real"));
        inbound.AddClaim(new Claim("email", "google@qehc.edu.sa"));
        inbound.AddClaim(new Claim("email_verified", "true"));
        inbound.AddClaim(new Claim("name", "Google User"));

        ClaimsPrincipal mapped = OidcPrincipalMapper.MapAuthenticatedPrincipal(new ClaimsPrincipal(inbound));

        Assert.Equal("google-sub-real", mapped.FindFirst(OidcPrincipalMapper.ExternalIdClaimType)?.Value);
        Assert.Equal("google@qehc.edu.sa", mapped.FindFirst(OidcPrincipalMapper.UpnClaimType)?.Value);
        Assert.DoesNotContain(mapped.Claims, claim => claim.Value is "entra-object-id" or "ms-oid" or "entra@qehc.edu.sa");
    }

    private static ClaimsPrincipal CreateGooglePrincipal(
        string sub,
        string email,
        string name,
        string emailVerified)
    {
        ClaimsIdentity inbound = new("oidc");
        inbound.AddClaim(new Claim("sub", sub));
        inbound.AddClaim(new Claim("email", email));
        inbound.AddClaim(new Claim("email_verified", emailVerified));
        inbound.AddClaim(new Claim("name", name));
        return new ClaimsPrincipal(inbound);
    }
}
