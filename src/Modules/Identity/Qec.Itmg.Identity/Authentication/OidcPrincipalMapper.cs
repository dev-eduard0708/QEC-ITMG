using System.Security.Claims;

namespace Qec.Itmg.Identity.Authentication;

/// <summary>
/// Maps inbound OIDC claims into the application cookie principal.
/// IdP roles/groups are intentionally discarded — ITMG authorization is SQL RBAC.
/// </summary>
public static class OidcPrincipalMapper
{
    public const string ExternalIdClaimType = "qec_external_id";
    public const string UpnClaimType = "upn";

    public static ClaimsPrincipal MapAuthenticatedPrincipal(ClaimsPrincipal inbound)
    {
        ArgumentNullException.ThrowIfNull(inbound);

        ClaimsIdentity? source = inbound.Identities.FirstOrDefault(static identity => identity.IsAuthenticated)
            ?? inbound.Identity as ClaimsIdentity
            ?? throw new InvalidOperationException("Authenticated OIDC principal is required.");

        string externalId = ResolveExternalId(source);
        string? upn = ResolveUpn(source);
        string? displayName = ResolveDisplayName(source);

        ClaimsIdentity mapped = new(
            authenticationType: source.AuthenticationType ?? "oidc",
            nameType: ClaimTypes.Name,
            roleType: "qec_no_idp_roles");

        mapped.AddClaim(new Claim(ExternalIdClaimType, externalId));
        mapped.AddClaim(new Claim(ClaimTypes.NameIdentifier, externalId));

        if (!string.IsNullOrWhiteSpace(upn))
        {
            mapped.AddClaim(new Claim(UpnClaimType, upn));
            mapped.AddClaim(new Claim(ClaimTypes.Upn, upn));
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            mapped.AddClaim(new Claim(ClaimTypes.Name, displayName));
        }

        return new ClaimsPrincipal(mapped);
    }

    public static bool ContainsAuthorizationRoleClaims(ClaimsPrincipal principal) =>
        principal.Claims.Any(static claim =>
            claim.Type is ClaimTypes.Role or "roles" or "role" or "groups"
            || claim.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase)
            || claim.Type.EndsWith("/roles", StringComparison.OrdinalIgnoreCase)
            || claim.Type.EndsWith("/groups", StringComparison.OrdinalIgnoreCase));

    private static string ResolveExternalId(ClaimsIdentity identity)
    {
        string? value = identity.FindFirst("oid")?.Value
            ?? identity.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
            ?? identity.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("OIDC principal is missing both oid and sub claims.");
        }

        return value;
    }

    private static string? ResolveUpn(ClaimsIdentity identity) =>
        identity.FindFirst("preferred_username")?.Value
        ?? identity.FindFirst("upn")?.Value
        ?? identity.FindFirst(ClaimTypes.Upn)?.Value;

    private static string? ResolveDisplayName(ClaimsIdentity identity) =>
        identity.FindFirst("name")?.Value
        ?? identity.FindFirst(ClaimTypes.Name)?.Value;
}
