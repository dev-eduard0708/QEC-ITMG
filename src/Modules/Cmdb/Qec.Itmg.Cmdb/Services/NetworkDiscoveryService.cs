using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb.Discovery;
using Qec.Itmg.Cmdb.Domain;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Numbering;

namespace Qec.Itmg.Cmdb.Services;

public sealed record NetworkDiscoveryProfileDto(
    Guid Id,
    string Name,
    string Cidr,
    Guid? LocationId,
    bool IsActive,
    int TimeoutMs,
    int MaxConcurrency,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record NetworkDiscoveryRunDto(
    Guid Id,
    Guid ProfileId,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    Guid StartedByUserId,
    int AddressesScanned,
    int ResponsiveHosts,
    string? ErrorSummary,
    int ExpectedAddressCount);

public sealed record NetworkDiscoveryObservationDto(
    Guid Id,
    Guid RunId,
    string IpAddress,
    string? Hostname,
    string? MacAddress,
    string? Vendor,
    int? ResponseMs,
    string MatchStatus,
    Guid? MatchedConfigurationItemId,
    string? MatchedCiName,
    DateTimeOffset ObservedAtUtc,
    string ReviewStatus);

public sealed class NetworkDiscoveryService(
    CmdbDbContext db,
    IClock clock,
    INetworkDiscoveryProvider discoveryProvider,
    INetworkDiscoveryRunQueue runQueue,
    INumberSequenceService numbers,
    IBusinessAuditWriter businessAudit)
{
    public async Task<IReadOnlyList<NetworkDiscoveryProfileDto>> ListProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        List<NetworkDiscoveryProfile> items = await db.NetworkDiscoveryProfiles.AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return items.Select(MapProfile).ToList();
    }

    public async Task<NetworkDiscoveryProfileDto?> GetProfileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        NetworkDiscoveryProfile? item = await db.NetworkDiscoveryProfiles.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        return item is null ? null : MapProfile(item);
    }

    public async Task<NetworkDiscoveryProfileDto> CreateProfileAsync(
        string name,
        string cidr,
        Guid? locationId,
        int? timeoutMs,
        int? maxConcurrency,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        _ = IcmpDnsDiscoveryProvider.ExpandCidr(cidr);
        NetworkDiscoveryProfile profile = NetworkDiscoveryProfile.Create(
            name,
            cidr.Trim(),
            clock.UtcNow,
            locationId,
            timeoutMs ?? 1000,
            maxConcurrency ?? 32,
            isActive);
        db.NetworkDiscoveryProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);
        await businessAudit.AppendAsync(
            CmdbAudit.Created(profile.Id, profile.Name, "DiscoveryProfileCreated"),
            cancellationToken);
        return MapProfile(profile);
    }

    public async Task<NetworkDiscoveryProfileDto> UpdateProfileAsync(
        Guid id,
        string name,
        string cidr,
        Guid? locationId,
        bool isActive,
        int timeoutMs,
        int maxConcurrency,
        CancellationToken cancellationToken = default)
    {
        NetworkDiscoveryProfile profile = await db.NetworkDiscoveryProfiles
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Discovery profile was not found.");

        _ = IcmpDnsDiscoveryProvider.ExpandCidr(cidr);
        profile.Update(name, cidr.Trim(), locationId, isActive, timeoutMs, maxConcurrency, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return MapProfile(profile);
    }

    public async Task<bool> DeleteProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        NetworkDiscoveryProfile? profile = await db.NetworkDiscoveryProfiles
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null)
        {
            return false;
        }

        bool hasRuns = await db.NetworkDiscoveryRuns.AsNoTracking()
            .AnyAsync(item => item.ProfileId == id, cancellationToken);
        if (hasRuns)
        {
            profile.Update(
                profile.Name,
                profile.Cidr,
                profile.LocationId,
                isActive: false,
                profile.TimeoutMs,
                profile.MaxConcurrency,
                clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        db.NetworkDiscoveryProfiles.Remove(profile);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<NetworkDiscoveryRunDto> StartRunAsync(
        Guid profileId,
        Guid startedByUserId,
        CancellationToken cancellationToken = default)
    {
        NetworkDiscoveryProfile profile = await db.NetworkDiscoveryProfiles
            .FirstOrDefaultAsync(item => item.Id == profileId, cancellationToken)
            ?? throw new InvalidOperationException("Discovery profile was not found.");
        if (!profile.IsActive)
        {
            throw new InvalidOperationException("Discovery profile is inactive.");
        }

        int expected = IcmpDnsDiscoveryProvider.ExpandCidr(profile.Cidr).Count;
        NetworkDiscoveryRun run = NetworkDiscoveryRun.Start(profileId, startedByUserId, clock.UtcNow);
        db.NetworkDiscoveryRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        await businessAudit.AppendAsync(
            CmdbAudit.Created(run.Id, profile.Name, "DiscoveryRunStarted"),
            cancellationToken);
        await runQueue.EnqueueAsync(new NetworkDiscoveryRunWorkItem(run.Id), cancellationToken);
        return MapRun(run, expected);
    }

    public async Task ExecuteRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        NetworkDiscoveryRun? run = await db.NetworkDiscoveryRuns
            .FirstOrDefaultAsync(item => item.Id == runId, cancellationToken);
        if (run is null || run.Status is not NetworkDiscoveryRunStatus.Running)
        {
            return;
        }

        NetworkDiscoveryProfile? profile = await db.NetworkDiscoveryProfiles
            .FirstOrDefaultAsync(item => item.Id == run.ProfileId, cancellationToken);
        if (profile is null)
        {
            run.Fail("Discovery profile was deleted.", clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            IReadOnlyList<System.Net.IPAddress> addresses = IcmpDnsDiscoveryProvider.ExpandCidr(profile.Cidr);
            IReadOnlyList<NetworkHostObservation> observations = await discoveryProvider.DiscoverAsync(
                new NetworkDiscoveryScanRequest(profile.Cidr, profile.TimeoutMs, profile.MaxConcurrency),
                cancellationToken);

            List<CiNetworkIdentity> identities = await db.CiNetworkIdentities.AsNoTracking().ToListAsync(cancellationToken);
            Dictionary<string, List<CiNetworkIdentity>> byIp = identities
                .GroupBy(item => item.IpAddress, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<CiNetworkIdentity>> byHostname = identities
                .Where(item => !string.IsNullOrWhiteSpace(item.Hostname))
                .GroupBy(item => item.Hostname!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<CiNetworkIdentity>> byMac = identities
                .Where(item => !string.IsNullOrWhiteSpace(item.MacAddress))
                .GroupBy(item => item.MacAddress!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (NetworkHostObservation observation in observations)
            {
                (NetworkDiscoveryMatchStatus status, Guid? matchedCiId) = SuggestMatch(
                    observation,
                    byIp,
                    byHostname,
                    byMac);
                db.NetworkDiscoveryObservations.Add(NetworkDiscoveryObservation.Create(
                    run.Id,
                    observation.IpAddress,
                    clock.UtcNow,
                    observation.Hostname,
                    observation.MacAddress,
                    observation.Vendor,
                    observation.ResponseMs,
                    status,
                    matchedCiId));
            }

            run.Complete(addresses.Count, observations.Count, clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            await businessAudit.AppendAsync(
                CmdbAudit.Updated(
                    run.Id,
                    profile.Name,
                    "DiscoveryRunCompleted",
                    null,
                    $"{observations.Count} responsive / {addresses.Count} scanned"),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            run.Cancel(clock.UtcNow);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            run.Fail(ex.Message, clock.UtcNow);
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    public async Task<NetworkDiscoveryRunDto?> GetRunAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        NetworkDiscoveryRun? run = await db.NetworkDiscoveryRuns.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (run is null)
        {
            return null;
        }

        NetworkDiscoveryProfile? profile = await db.NetworkDiscoveryProfiles.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == run.ProfileId, cancellationToken);
        int expected = 0;
        if (profile is not null)
        {
            try
            {
                expected = IcmpDnsDiscoveryProvider.ExpandCidr(profile.Cidr).Count;
            }
            catch
            {
                expected = run.AddressesScanned;
            }
        }

        return MapRun(run, expected);
    }

    public async Task<IReadOnlyList<NetworkDiscoveryObservationDto>> ListObservationsAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        List<NetworkDiscoveryObservation> items = await db.NetworkDiscoveryObservations.AsNoTracking()
            .Where(item => item.RunId == runId)
            .OrderBy(item => item.IpAddress)
            .ToListAsync(cancellationToken);

        List<Guid> matchedIds = items
            .Where(item => item.MatchedConfigurationItemId.HasValue)
            .Select(item => item.MatchedConfigurationItemId!.Value)
            .Distinct()
            .ToList();
        Dictionary<Guid, string> names = await db.ConfigurationItems.AsNoTracking()
            .Where(item => matchedIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        return items.Select(item =>
        {
            string? matchedName = null;
            if (item.MatchedConfigurationItemId is Guid ciId)
            {
                names.TryGetValue(ciId, out matchedName);
            }

            return MapObservation(item, matchedName);
        }).ToList();
    }

    public async Task<NetworkDiscoveryObservationDto> MatchExistingAsync(
        Guid observationId,
        Guid configurationItemId,
        bool addIdentityIfMissing = true,
        CancellationToken cancellationToken = default)
    {
        NetworkDiscoveryObservation observation = await db.NetworkDiscoveryObservations
            .FirstOrDefaultAsync(item => item.Id == observationId, cancellationToken)
            ?? throw new InvalidOperationException("Observation was not found.");
        if (observation.ReviewStatus != NetworkDiscoveryReviewStatus.Pending)
        {
            throw new InvalidOperationException("Observation has already been reviewed.");
        }

        ConfigurationItem? ci = await db.ConfigurationItems
            .FirstOrDefaultAsync(item => item.Id == configurationItemId, cancellationToken)
            ?? throw new InvalidOperationException("Configuration item was not found.");

        if (addIdentityIfMissing)
        {
            bool hasIp = await db.CiNetworkIdentities.AsNoTracking()
                .AnyAsync(
                    item => item.ConfigurationItemId == configurationItemId
                        && item.IpAddress == observation.IpAddress,
                    cancellationToken);
            if (!hasIp)
            {
                bool anyIdentity = await db.CiNetworkIdentities.AsNoTracking()
                    .AnyAsync(item => item.ConfigurationItemId == configurationItemId, cancellationToken);
                db.CiNetworkIdentities.Add(CiNetworkIdentity.Create(
                    configurationItemId,
                    observation.IpAddress,
                    clock.UtcNow,
                    observation.Hostname,
                    observation.MacAddress,
                    isPrimary: !anyIdentity));
            }
        }

        observation.MarkMatched(configurationItemId);
        await db.SaveChangesAsync(cancellationToken);
        await businessAudit.AppendAsync(
            CmdbAudit.Linked(observation.Id, ci.CiNumber, "DiscoveryObservationMatched"),
            cancellationToken);
        return MapObservation(observation, ci.Name);
    }

    public async Task<NetworkDiscoveryObservationDto> CreateCiFromObservationAsync(
        Guid observationId,
        Guid ciTypeId,
        string name,
        Guid? locationId,
        string? description,
        CancellationToken cancellationToken = default)
    {
        NetworkDiscoveryObservation observation = await db.NetworkDiscoveryObservations
            .FirstOrDefaultAsync(item => item.Id == observationId, cancellationToken)
            ?? throw new InvalidOperationException("Observation was not found.");
        if (observation.ReviewStatus != NetworkDiscoveryReviewStatus.Pending)
        {
            throw new InvalidOperationException("Observation has already been reviewed.");
        }

        bool typeExists = await db.CiTypes.AsNoTracking()
            .AnyAsync(item => item.Id == ciTypeId && item.IsActive, cancellationToken);
        if (!typeExists)
        {
            throw new InvalidOperationException("CI type was not found or is inactive.");
        }

        string ciNumber = await numbers.NextAsync(
            ConfigurationItemService.CiSequenceKey,
            ConfigurationItemService.CiNumberPrefix,
            cancellationToken);
        ConfigurationItem ci = ConfigurationItem.Create(
            ciNumber,
            ciTypeId,
            name,
            clock.UtcNow,
            description,
            locationId: locationId,
            manufacturer: observation.Vendor,
            notes: null);
        db.ConfigurationItems.Add(ci);
        db.CiNetworkIdentities.Add(CiNetworkIdentity.Create(
            ci.Id,
            observation.IpAddress,
            clock.UtcNow,
            observation.Hostname,
            observation.MacAddress,
            isPrimary: true));
        observation.MarkCreated(ci.Id);
        await db.SaveChangesAsync(cancellationToken);
        await businessAudit.AppendAsync(
            CmdbAudit.Created(ci.Id, ci.CiNumber, "DiscoveryCiCreated"),
            cancellationToken);
        return MapObservation(observation, ci.Name);
    }

    public async Task<NetworkDiscoveryObservationDto> AcceptChangesAsync(
        Guid observationId,
        bool updateIp = true,
        bool updateHostname = true,
        bool updateMac = true,
        CancellationToken cancellationToken = default)
    {
        NetworkDiscoveryObservation observation = await db.NetworkDiscoveryObservations
            .FirstOrDefaultAsync(item => item.Id == observationId, cancellationToken)
            ?? throw new InvalidOperationException("Observation was not found.");
        if (observation.ReviewStatus != NetworkDiscoveryReviewStatus.Pending)
        {
            throw new InvalidOperationException("Observation has already been reviewed.");
        }

        Guid ciId = observation.MatchedConfigurationItemId
            ?? throw new InvalidOperationException("Observation has no matched configuration item to update.");
        ConfigurationItem ci = await db.ConfigurationItems
            .FirstOrDefaultAsync(item => item.Id == ciId, cancellationToken)
            ?? throw new InvalidOperationException("Matched configuration item was not found.");

        List<CiNetworkIdentity> identities = await db.CiNetworkIdentities
            .Where(item => item.ConfigurationItemId == ciId)
            .ToListAsync(cancellationToken);
        CiNetworkIdentity? identity = identities.FirstOrDefault(item =>
                string.Equals(item.IpAddress, observation.IpAddress, StringComparison.OrdinalIgnoreCase))
            ?? identities.FirstOrDefault(item => item.IsPrimary)
            ?? identities.FirstOrDefault();

        if (identity is null)
        {
            db.CiNetworkIdentities.Add(CiNetworkIdentity.Create(
                ciId,
                observation.IpAddress,
                clock.UtcNow,
                updateHostname ? observation.Hostname : null,
                updateMac ? observation.MacAddress : null,
                isPrimary: true));
        }
        else
        {
            identity.Update(
                updateIp ? observation.IpAddress : identity.IpAddress,
                updateHostname ? observation.Hostname ?? identity.Hostname : identity.Hostname,
                updateMac ? observation.MacAddress ?? identity.MacAddress : identity.MacAddress,
                identity.IsPrimary,
                clock.UtcNow);
        }

        observation.MarkUpdated(ciId);
        await db.SaveChangesAsync(cancellationToken);
        await businessAudit.AppendAsync(
            CmdbAudit.Updated(observation.Id, ci.CiNumber, "DiscoveryChangesAccepted"),
            cancellationToken);
        return MapObservation(observation, ci.Name);
    }

    public async Task<NetworkDiscoveryObservationDto> IgnoreAsync(
        Guid observationId,
        CancellationToken cancellationToken = default)
    {
        NetworkDiscoveryObservation observation = await db.NetworkDiscoveryObservations
            .FirstOrDefaultAsync(item => item.Id == observationId, cancellationToken)
            ?? throw new InvalidOperationException("Observation was not found.");
        if (observation.ReviewStatus != NetworkDiscoveryReviewStatus.Pending)
        {
            throw new InvalidOperationException("Observation has already been reviewed.");
        }

        observation.MarkIgnored();
        await db.SaveChangesAsync(cancellationToken);
        await businessAudit.AppendAsync(
            CmdbAudit.Updated(observation.Id, observation.IpAddress, "DiscoveryObservationIgnored"),
            cancellationToken);
        return MapObservation(observation, null);
    }

    private static (NetworkDiscoveryMatchStatus Status, Guid? MatchedCiId) SuggestMatch(
        NetworkHostObservation observation,
        Dictionary<string, List<CiNetworkIdentity>> byIp,
        Dictionary<string, List<CiNetworkIdentity>> byHostname,
        Dictionary<string, List<CiNetworkIdentity>> byMac)
    {
        if (byIp.TryGetValue(observation.IpAddress, out List<CiNetworkIdentity>? ipMatches)
            && ipMatches.Count > 0)
        {
            CiNetworkIdentity match = ipMatches[0];
            bool hostnameDiffers = !string.IsNullOrWhiteSpace(observation.Hostname)
                && !string.IsNullOrWhiteSpace(match.Hostname)
                && !string.Equals(observation.Hostname, match.Hostname, StringComparison.OrdinalIgnoreCase);
            return hostnameDiffers
                ? (NetworkDiscoveryMatchStatus.Changed, match.ConfigurationItemId)
                : (NetworkDiscoveryMatchStatus.Matched, match.ConfigurationItemId);
        }

        if (!string.IsNullOrWhiteSpace(observation.MacAddress)
            && byMac.TryGetValue(observation.MacAddress, out List<CiNetworkIdentity>? macMatches)
            && macMatches.Count == 1)
        {
            CiNetworkIdentity match = macMatches[0];
            bool ipDiffers = !string.Equals(observation.IpAddress, match.IpAddress, StringComparison.OrdinalIgnoreCase);
            return ipDiffers
                ? (NetworkDiscoveryMatchStatus.Changed, match.ConfigurationItemId)
                : (NetworkDiscoveryMatchStatus.Matched, match.ConfigurationItemId);
        }

        if (!string.IsNullOrWhiteSpace(observation.Hostname)
            && byHostname.TryGetValue(observation.Hostname, out List<CiNetworkIdentity>? hostMatches)
            && hostMatches.Count > 0)
        {
            return (NetworkDiscoveryMatchStatus.PossibleMatch, hostMatches[0].ConfigurationItemId);
        }

        return (NetworkDiscoveryMatchStatus.New, null);
    }

    private static NetworkDiscoveryProfileDto MapProfile(NetworkDiscoveryProfile item) =>
        new(
            item.Id,
            item.Name,
            item.Cidr,
            item.LocationId,
            item.IsActive,
            item.TimeoutMs,
            item.MaxConcurrency,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);

    private static NetworkDiscoveryRunDto MapRun(NetworkDiscoveryRun run, int expectedAddressCount) =>
        new(
            run.Id,
            run.ProfileId,
            run.Status.ToString(),
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.StartedByUserId,
            run.AddressesScanned,
            run.ResponsiveHosts,
            run.ErrorSummary,
            expectedAddressCount);

    private static NetworkDiscoveryObservationDto MapObservation(
        NetworkDiscoveryObservation item,
        string? matchedCiName) =>
        new(
            item.Id,
            item.RunId,
            item.IpAddress,
            item.Hostname,
            item.MacAddress,
            item.Vendor,
            item.ResponseMs,
            item.MatchStatus.ToString(),
            item.MatchedConfigurationItemId,
            matchedCiName,
            item.ObservedAtUtc,
            item.ReviewStatus.ToString());
}
