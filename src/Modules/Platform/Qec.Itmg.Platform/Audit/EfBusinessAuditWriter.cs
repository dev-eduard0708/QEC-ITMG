using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Platform.Audit;

public sealed class EfBusinessAuditWriter(
    PlatformDbContext db,
    IClock clock,
    IAuditRequestContext requestContext) : IBusinessAuditWriter
{
    public async ValueTask AppendAsync(BusinessAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        db.BusinessAuditRecords.Add(await CreateRecordAsync(entry, cancellationToken));
    }

    public async ValueTask AppendManyAsync(
        IEnumerable<BusinessAuditEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (BusinessAuditEntry entry in entries)
        {
            db.BusinessAuditRecords.Add(await CreateRecordAsync(entry, cancellationToken));
        }
    }

    private async Task<BusinessAuditRecord> CreateRecordAsync(
        BusinessAuditEntry entry,
        CancellationToken cancellationToken)
    {
        Guid? actorUserId = await requestContext.GetActorUserIdAsync(cancellationToken);
        return BusinessAuditRecord.Create(
            entry,
            clock.UtcNow,
            actorUserId,
            requestContext.ActorType,
            requestContext.JobName,
            requestContext.CorrelationId,
            requestContext.ClientIp);
    }
}
