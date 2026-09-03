using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Identity.Authentication;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;

namespace Qec.Itmg.Identity.Audit;

public sealed class IdentityAuditRequestContext(
    IHttpContextAccessor httpContextAccessor,
    IdentityDbContext db) : IAuditRequestContext
{
    private Guid? _actorUserId;
    private bool _resolved;

    public AuditActorType ActorType =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true
            ? AuditActorType.User
            : AuditActorType.System;

    public string? JobName => null;

    public string? CorrelationId =>
        httpContextAccessor.HttpContext?.TraceIdentifier;

    public string? ClientIp =>
        httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public async Task<Guid?> GetActorUserIdAsync(CancellationToken cancellationToken = default)
    {
        if (_resolved)
        {
            return _actorUserId;
        }

        ClaimsPrincipal? principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            _resolved = true;
            _actorUserId = null;
            return null;
        }

        _actorUserId = await ResolveUserIdAsync(principal, cancellationToken);
        _resolved = true;
        return _actorUserId;
    }

    private async Task<Guid?> ResolveUserIdAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        string? externalId = principal.FindFirstValue(OidcPrincipalMapper.ExternalIdClaimType)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(externalId))
        {
            Guid? byDirectory = await db.Users
                .AsNoTracking()
                .Where(user => user.DirectoryObjectId == externalId)
                .Select(user => (Guid?)user.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (byDirectory is not null)
            {
                return byDirectory;
            }
        }

        string? upn = principal.FindFirstValue(OidcPrincipalMapper.UpnClaimType)
            ?? principal.FindFirstValue(ClaimTypes.Upn);
        if (string.IsNullOrWhiteSpace(upn))
        {
            return null;
        }

        return await db.Users
            .AsNoTracking()
            .Where(user => user.Upn == upn)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

/// <summary>
/// Test/default context when no HTTP request is present.
/// </summary>
public sealed class NullAuditRequestContext : IAuditRequestContext
{
    public static NullAuditRequestContext Instance { get; } = new();

    public AuditActorType ActorType => AuditActorType.System;

    public string? JobName => null;

    public string? CorrelationId => null;

    public string? ClientIp => null;

    public Task<Guid?> GetActorUserIdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<Guid?>(null);
}
