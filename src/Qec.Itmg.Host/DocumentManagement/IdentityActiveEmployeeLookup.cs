using Microsoft.EntityFrameworkCore;
using Qec.Itmg.Contracts.Identity;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;

namespace Qec.Itmg.Host.DocumentManagement;

public sealed class IdentityActiveEmployeeLookup(IdentityDbContext db) : IActiveEmployeeLookup
{
    public async Task<IReadOnlyList<ActiveEmployeeInfo>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        return await db.Users.AsNoTracking()
            .Where(x => x.Status == UserStatus.Active)
            .OrderBy(x => x.DisplayName)
            .Select(x => new ActiveEmployeeInfo(x.Id, x.Upn, x.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public async Task<ActiveEmployeeInfo?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new ActiveEmployeeInfo(x.Id, x.Upn, x.DisplayName))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
