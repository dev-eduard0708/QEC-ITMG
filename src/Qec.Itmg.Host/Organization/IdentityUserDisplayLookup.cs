using Microsoft.EntityFrameworkCore;
using Qec.Itmg.Contracts.Identity;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;

namespace Qec.Itmg.Host.Organization;

public sealed class IdentityUserDisplayLookup(IdentityDbContext db) : IUserDisplayLookup
{
    public async Task<IReadOnlyDictionary<Guid, UserDisplayInfo>> GetManyAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        List<Guid> idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<Guid, UserDisplayInfo>();
        }

        List<UserDisplayInfo> users = await db.Users.AsNoTracking()
            .Where(x => idList.Contains(x.Id))
            .Select(x => new UserDisplayInfo(
                x.Id,
                x.Upn,
                x.DisplayName,
                x.Status == UserStatus.Active,
                x.ProfileImageUrl))
            .ToListAsync(cancellationToken);

        return users.ToDictionary(x => x.Id);
    }
}
