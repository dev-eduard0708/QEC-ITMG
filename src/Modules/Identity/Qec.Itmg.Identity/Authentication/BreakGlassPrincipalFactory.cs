using System.Security.Claims;
using Qec.Itmg.Identity.Domain;

namespace Qec.Itmg.Identity.Authentication;

public static class BreakGlassPrincipalFactory
{
    public const string AuthMethodClaimType = "qec_auth_method";
    public const string AuthMethodBreakGlass = "break-glass";

    public static ClaimsPrincipal Create(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        string externalId = string.IsNullOrWhiteSpace(user.DirectoryObjectId)
            ? $"break-glass:{user.Id:N}"
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
        identity.AddClaim(new Claim(AuthMethodClaimType, AuthMethodBreakGlass));
        identity.AddClaim(new Claim(ClaimTypes.AuthenticationMethod, AuthMethodBreakGlass));

        return new ClaimsPrincipal(identity);
    }

    public static bool IsBreakGlass(ClaimsPrincipal? principal) =>
        principal is not null
        && (string.Equals(
                principal.FindFirstValue(AuthMethodClaimType),
                AuthMethodBreakGlass,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                principal.FindFirstValue(ClaimTypes.AuthenticationMethod),
                AuthMethodBreakGlass,
                StringComparison.OrdinalIgnoreCase));
}
