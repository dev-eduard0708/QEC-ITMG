using System.Security.Claims;

namespace Qec.Itmg.Identity.Authentication;

public static class DevelopmentLoginPrincipalFactory
{
    public const string AuthMethodDevelopment = "Development";

    public const string AdminUpn = "dev.admin@itmg.local";
    public const string AdminDisplayName = "Development Administrator";
    public const string AdminExternalId = "dev:admin";

    public const string EmployeeUpn = "dev.employee@itmg.local";
    public const string EmployeeDisplayName = "Development Employee";
    public const string EmployeeExternalId = "dev:employee";

    public static ClaimsPrincipal Create(string upn, string displayName, string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(upn);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        ClaimsIdentity identity = new(
            authenticationType: IdentityAuthenticationExtensions.CookieScheme,
            nameType: ClaimTypes.Name,
            roleType: "qec_no_idp_roles");

        identity.AddClaim(new Claim(OidcPrincipalMapper.ExternalIdClaimType, externalId));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, externalId));
        identity.AddClaim(new Claim(OidcPrincipalMapper.UpnClaimType, upn));
        identity.AddClaim(new Claim(ClaimTypes.Upn, upn));
        identity.AddClaim(new Claim(ClaimTypes.Email, upn));
        identity.AddClaim(new Claim(ClaimTypes.Name, displayName));
        identity.AddClaim(new Claim(BreakGlassPrincipalFactory.AuthMethodClaimType, AuthMethodDevelopment));
        identity.AddClaim(new Claim(ClaimTypes.AuthenticationMethod, AuthMethodDevelopment));

        return new ClaimsPrincipal(identity);
    }

    public static bool IsDevelopment(ClaimsPrincipal? principal) =>
        principal is not null
        && (string.Equals(
                principal.FindFirstValue(BreakGlassPrincipalFactory.AuthMethodClaimType),
                AuthMethodDevelopment,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                principal.FindFirstValue(ClaimTypes.AuthenticationMethod),
                AuthMethodDevelopment,
                StringComparison.OrdinalIgnoreCase));
}
