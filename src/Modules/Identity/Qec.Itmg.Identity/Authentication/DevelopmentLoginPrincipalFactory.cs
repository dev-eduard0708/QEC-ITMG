using System.Security.Claims;
using Qec.Itmg.Identity.Domain;

namespace Qec.Itmg.Identity.Authentication;

/// <summary>
/// Stable Development-only principal helpers for local quick login.
/// Identities are not seeded from configuration; users are provisioned on first login.
/// </summary>
public static class DevelopmentLoginPrincipalFactory
{
    public const string AuthMethodDevelopment = "Development";

    public const string AdminUpn = "dev.admin@itmg.local";
    public const string AdminDisplayName = "Local Admin";
    public const string AdminExternalId = "dev:admin";

    public const string EmployeeUpn = "dev.employee@itmg.local";
    public const string EmployeeDisplayName = "Local Employee";
    public const string EmployeeExternalId = "dev:employee";

    public static ClaimsPrincipal Create(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        string externalId = string.IsNullOrWhiteSpace(user.DirectoryObjectId)
            ? $"dev:{user.Id:N}"
            : user.DirectoryObjectId;

        ClaimsIdentity identity = new(
            authenticationType: IdentityAuthenticationExtensions.CookieScheme,
            nameType: ClaimTypes.Name,
            roleType: "qec_no_idp_roles");

        identity.AddClaim(new Claim(OidcPrincipalMapper.ExternalIdClaimType, externalId));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, externalId));
        identity.AddClaim(new Claim(OidcPrincipalMapper.UpnClaimType, user.Upn));
        identity.AddClaim(new Claim(ClaimTypes.Upn, user.Upn));
        identity.AddClaim(new Claim(ClaimTypes.Email, user.Upn));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.DisplayName));
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
