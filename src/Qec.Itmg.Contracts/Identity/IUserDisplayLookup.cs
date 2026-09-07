namespace Qec.Itmg.Contracts.Identity;

public sealed record UserDisplayInfo(
    Guid Id,
    string Upn,
    string DisplayName,
    bool IsActive,
    string? AvatarUrl);

/// <summary>Resolves display metadata for one or many users (active or disabled).</summary>
public interface IUserDisplayLookup
{
    Task<IReadOnlyDictionary<Guid, UserDisplayInfo>> GetManyAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);
}
