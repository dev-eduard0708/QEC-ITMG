using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Platform.Audit;

public sealed class EfSecurityAuditLogger(
    PlatformDbContext db,
    IClock clock,
    IAuditRequestContext requestContext) : ISecurityAuditLogger
{
    public async ValueTask AppendAsync(SecurityAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        db.SecurityAuditEvents.Add(await CreateEventAsync(entry, cancellationToken));
    }

    public async Task WriteImmediateAsync(SecurityAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        db.SecurityAuditEvents.Add(await CreateEventAsync(entry, cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<SecurityAuditEvent> CreateEventAsync(
        SecurityAuditEntry entry,
        CancellationToken cancellationToken)
    {
        Guid? actorUserId = await requestContext.GetActorUserIdAsync(cancellationToken);
        return SecurityAuditEvent.Create(
            entry,
            clock.UtcNow,
            actorUserId,
            requestContext.CorrelationId,
            requestContext.ClientIp);
    }
}
