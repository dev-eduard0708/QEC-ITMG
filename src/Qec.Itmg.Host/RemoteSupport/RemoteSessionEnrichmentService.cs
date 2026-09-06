using Microsoft.EntityFrameworkCore;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.RemoteSupport.Services;

namespace Qec.Itmg.Host.RemoteSupport;

/// <summary>
/// Adds friendly employee/technician/device fields for Support queue and detail UIs.
/// </summary>
public sealed class RemoteSessionEnrichmentService(
    IdentityDbContext identityDb,
    RemoteEndpointService endpoints)
{
    public async Task<RemoteSessionRequestDto> EnrichAsync(
        RemoteSessionRequestDto item,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RemoteSessionRequestDto> list = await EnrichManyAsync([item], cancellationToken);
        return list[0];
    }

    public async Task<IReadOnlyList<RemoteSessionRequestDto>> EnrichManyAsync(
        IReadOnlyList<RemoteSessionRequestDto> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
            return items;

        HashSet<Guid> userIds = [];
        HashSet<Guid> endpointIds = [];
        foreach (RemoteSessionRequestDto item in items)
        {
            userIds.Add(item.RequestedByUserId);
            if (item.TargetUserId is Guid target)
                userIds.Add(target);
            if (item.TechnicianUserId is Guid tech)
                userIds.Add(tech);
            if (item.RemoteEndpointId is Guid ep)
                endpointIds.Add(ep);
        }

        Dictionary<Guid, (string DisplayName, string Upn)> users = await identityDb.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Upn })
            .ToDictionaryAsync(u => u.Id, u => (u.DisplayName, u.Upn), cancellationToken);

        Dictionary<Guid, RemoteEndpointDto> endpointsById = new();
        foreach (Guid endpointId in endpointIds)
        {
            RemoteEndpointDto? ep = await endpoints.GetAsync(endpointId, cancellationToken);
            if (ep is not null)
                endpointsById[endpointId] = ep;
        }

        // For active requests without a bound endpoint, surface the owner's best endpoint summary.
        Dictionary<Guid, RemoteEndpointDto> ownerBest = new();
        foreach (RemoteSessionRequestDto item in items.Where(i =>
                     i.RemoteEndpointId is null
                     && i.TargetUserId is not null
                     && i.Status is not ("Ended" or "Declined" or "Expired")))
        {
            Guid ownerId = item.TargetUserId!.Value;
            if (ownerBest.ContainsKey(ownerId))
                continue;
            IReadOnlyList<RemoteEndpointDto> owned = await endpoints.ListForOwnerAsync(
                ownerId, syncPresence: false, cancellationToken);
            RemoteEndpointDto? best = owned
                .OrderByDescending(e => e.IsReadyForRemote)
                .ThenByDescending(e => e.LastSeenAtUtc)
                .FirstOrDefault();
            if (best is not null)
                ownerBest[ownerId] = best;
        }

        List<RemoteSessionRequestDto> result = new(items.Count);
        foreach (RemoteSessionRequestDto item in items)
        {
            Guid employeeId = item.TargetUserId ?? item.RequestedByUserId;
            users.TryGetValue(employeeId, out (string DisplayName, string Upn) employee);
            string? techName = null;
            if (item.TechnicianUserId is Guid techId && users.TryGetValue(techId, out var tech))
                techName = tech.DisplayName;

            RemoteEndpointDto? endpoint = null;
            if (item.RemoteEndpointId is Guid boundId)
                endpointsById.TryGetValue(boundId, out endpoint);
            else if (item.TargetUserId is Guid owner && ownerBest.TryGetValue(owner, out RemoteEndpointDto? best))
                endpoint = best;

            result.Add(item with
            {
                EmployeeDisplayName = string.IsNullOrWhiteSpace(employee.DisplayName) ? null : employee.DisplayName,
                EmployeeEmail = string.IsNullOrWhiteSpace(employee.Upn) ? null : employee.Upn,
                TechnicianDisplayName = techName,
                EndpointDeviceName = endpoint?.DeviceName,
                EndpointOperatingSystem = FormatOs(endpoint),
                EndpointArchitecture = endpoint?.Architecture,
                EndpointConnectionStatus = endpoint?.ConnectionStatus,
                EndpointKind = endpoint?.EndpointKind,
                EndpointIsReady = endpoint?.IsReadyForRemote,
                EndpointLastSeenAtUtc = endpoint?.LastSeenAtUtc,
            });
        }

        return result;
    }

    private static string? FormatOs(RemoteEndpointDto? endpoint)
    {
        if (endpoint is null) return null;
        string os = endpoint.OperatingSystem;
        if (!string.IsNullOrWhiteSpace(endpoint.OperatingSystemVersion)
            && !os.Contains(endpoint.OperatingSystemVersion, StringComparison.OrdinalIgnoreCase))
            os = $"{os} {endpoint.OperatingSystemVersion}".Trim();
        if (!string.IsNullOrWhiteSpace(endpoint.Architecture))
            os = $"{os} {endpoint.Architecture}".Trim();
        return os;
    }
}
