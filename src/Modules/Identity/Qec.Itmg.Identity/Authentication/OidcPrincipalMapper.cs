using System.Security.Claims;

namespace Qec.Itmg.Identity.Authentication;

/// <summary>
/// Maps inbound OIDC claims into the application cookie principal.
/// Primary provider is Google OIDC (sub/email/name). IdP roles/groups are discarded — ITMG authorization is SQL RBAC.
/// </summary>
public static class OidcPrincipalMapper
{
    public const string ExternalIdClaimType = "qec_external_id";
    public const string UpnClaimType = "upn";
    public const string AvatarUrlClaimType = "qec_avatar_url";

    public static ClaimsPrincipal MapAuthenticatedPrincipal(
        ClaimsPrincipal inbound,
        IReadOnlyList<string>? allowedDomains = null)
    {
        ArgumentNullException.ThrowIfNull(inbound);

        ClaimsIdentity? source = inbound.Identities.FirstOrDefault(static identity => identity.IsAuthenticated)
            ?? inbound.Identity as ClaimsIdentity
            ?? throw new InvalidOperationException("Authenticated OIDC principal is required.");

        EnsureEmailVerified(source);

        string externalId = ResolveExternalId(source);
        string email = ResolveEmail(source);
        EnsureAllowedDomain(email, allowedDomains);
        string? displayName = ResolveDisplayName(source);
        string? avatarUrl = ResolveAvatarUrl(source);

        ClaimsIdentity mapped = new(
            authenticationType: source.AuthenticationType ?? "oidc",
            nameType: ClaimTypes.Name,
            roleType: "qec_no_idp_roles");

        mapped.AddClaim(new Claim(ExternalIdClaimType, externalId));
        mapped.AddClaim(new Claim(ClaimTypes.NameIdentifier, externalId));
        mapped.AddClaim(new Claim(UpnClaimType, email));
        mapped.AddClaim(new Claim(ClaimTypes.Upn, email));
        mapped.AddClaim(new Claim(ClaimTypes.Email, email));

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            mapped.AddClaim(new Claim(ClaimTypes.Name, displayName));
        }

        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            mapped.AddClaim(new Claim(AvatarUrlClaimType, avatarUrl));
        }

        return new ClaimsPrincipal(mapped);
    }

    public static bool ContainsAuthorizationRoleClaims(ClaimsPrincipal principal) =>
        principal.Claims.Any(static claim =>
            claim.Type is ClaimTypes.Role or "roles" or "role" or "groups"
            || claim.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase)
            || claim.Type.EndsWith("/roles", StringComparison.OrdinalIgnoreCase)
            || claim.Type.EndsWith("/groups", StringComparison.OrdinalIgnoreCase));

    private static void EnsureEmailVerified(ClaimsIdentity identity)
    {
        string? verified = identity.FindFirst("email_verified")?.Value;
        if (!IsTruthy(verified))
        {
            throw new InvalidOperationException("OIDC principal email_verified must be true.");
        }
    }

    private static string ResolveExternalId(ClaimsIdentity identity)
    {
        string? value = identity.FindFirst("sub")?.Value
            ?? identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("OIDC principal is missing the sub claim.");
        }

        return value.Trim();
    }

    private static string ResolveEmail(ClaimsIdentity identity)
    {
        string? value = identity.FindFirst("email")?.Value
            ?? identity.FindFirst(ClaimTypes.Email)?.Value;

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("OIDC principal is missing the email claim.");
        }

        return value.Trim();
    }

    private static string? ResolveDisplayName(ClaimsIdentity identity) =>
        identity.FindFirst("name")?.Value
        ?? identity.FindFirst(ClaimTypes.Name)?.Value;

    private static string? ResolveAvatarUrl(ClaimsIdentity identity)
    {
        string? value = identity.FindFirst("picture")?.Value
            ?? identity.FindFirst("profile")?.Value
            ?? identity.FindFirst(AvatarUrlClaimType)?.Value;

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return trimmed;
    }

    private static void EnsureAllowedDomain(string email, IReadOnlyList<string>? allowedDomains)
    {
        if (allowedDomains is null || allowedDomains.Count == 0)
        {
            return;
        }

        int at = email.LastIndexOf('@');
        if (at <= 0 || at == email.Length - 1)
        {
            throw new InvalidOperationException("OIDC email claim is not a valid address.");
        }

        string domain = email[(at + 1)..];
        bool allowed = allowedDomains.Any(allowedDomain =>
            string.Equals(allowedDomain.Trim(), domain, StringComparison.OrdinalIgnoreCase));

        if (!allowed)
        {
            throw new InvalidOperationException(
                $"Email domain '{domain}' is not in Authentication:Oidc:AllowedDomains.");
        }
    }

    private static bool IsTruthy(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
}
