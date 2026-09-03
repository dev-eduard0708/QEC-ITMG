using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Qec.Itmg.Contracts.Audit;

namespace Qec.Itmg.Identity.Audit;

internal static class SecurityAuditHooks
{
    public static async Task LogLoginSuccessAsync(HttpContext httpContext)
    {
        ISecurityAuditLogger? logger = httpContext.RequestServices.GetService(typeof(ISecurityAuditLogger)) as ISecurityAuditLogger;
        if (logger is null)
        {
            return;
        }

        await logger.WriteImmediateAsync(new SecurityAuditEntry
        {
            EventType = SecurityEventType.LoginSuccess,
            Outcome = SecurityEventOutcome.Success,
            Details = "Cookie session established",
        });
    }

    public static async Task LogLoginFailureAsync(HttpContext httpContext, string? details)
    {
        ISecurityAuditLogger? logger = httpContext.RequestServices.GetService(typeof(ISecurityAuditLogger)) as ISecurityAuditLogger;
        if (logger is null)
        {
            return;
        }

        await logger.WriteImmediateAsync(new SecurityAuditEntry
        {
            EventType = SecurityEventType.LoginFailure,
            Outcome = SecurityEventOutcome.Failure,
            Details = string.IsNullOrWhiteSpace(details) ? "Authentication failed" : details,
        });
    }

    public static async Task LogLogoutAsync(HttpContext httpContext)
    {
        ISecurityAuditLogger? logger = httpContext.RequestServices.GetService(typeof(ISecurityAuditLogger)) as ISecurityAuditLogger;
        if (logger is null)
        {
            return;
        }

        await logger.WriteImmediateAsync(new SecurityAuditEntry
        {
            EventType = SecurityEventType.Logout,
            Outcome = SecurityEventOutcome.Success,
        });
    }

    public static async Task LogPermissionDeniedAsync(HttpContext? httpContext, string permissionKey)
    {
        if (httpContext is null)
        {
            return;
        }

        ISecurityAuditLogger? logger = httpContext.RequestServices.GetService(typeof(ISecurityAuditLogger)) as ISecurityAuditLogger;
        if (logger is null)
        {
            return;
        }

        await logger.WriteImmediateAsync(new SecurityAuditEntry
        {
            EventType = SecurityEventType.PermissionDenied,
            Outcome = SecurityEventOutcome.Denied,
            Details = $"Permission:{permissionKey}",
        });
    }
}
