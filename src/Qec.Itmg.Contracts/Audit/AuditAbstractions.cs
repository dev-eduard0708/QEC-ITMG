namespace Qec.Itmg.Contracts.Audit;

/// <summary>
/// Request-scoped actor/correlation metadata for audit writers.
/// Implementations must resolve actor from the authenticated session, never from request bodies.
/// </summary>
public interface IAuditRequestContext
{
    Task<Guid?> GetActorUserIdAsync(CancellationToken cancellationToken = default);

    AuditActorType ActorType { get; }

    string? JobName { get; }

    string? CorrelationId { get; }

    string? ClientIp { get; }
}

/// <summary>
/// Stages append-only business history rows. Persistence occurs via <see cref="ISharedDbTransaction"/>.
/// </summary>
public interface IBusinessAuditWriter
{
    ValueTask AppendAsync(BusinessAuditEntry entry, CancellationToken cancellationToken = default);

    ValueTask AppendManyAsync(IEnumerable<BusinessAuditEntry> entries, CancellationToken cancellationToken = default);
}

/// <summary>
/// Security audit logger. Use <see cref="AppendAsync"/> inside a shared business transaction,
/// or <see cref="WriteImmediateAsync"/> for standalone authn/authz events.
/// </summary>
public interface ISecurityAuditLogger
{
    ValueTask AppendAsync(SecurityAuditEntry entry, CancellationToken cancellationToken = default);

    Task WriteImmediateAsync(SecurityAuditEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs module work and commits Identity + Platform changes in one SQL transaction.
/// If Platform audit inserts fail, Identity mutations roll back.
/// </summary>
public interface ISharedDbTransaction
{
    Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default);
}
