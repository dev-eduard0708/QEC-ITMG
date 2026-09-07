using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb.Domain;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.Contracts.Audit;

namespace Qec.Itmg.Cmdb.Services;

public sealed record NetworkTopologyViewDto(
    Guid Id,
    string Name,
    Guid? LocationId,
    string? Description,
    bool IsDefault,
    string RowVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record TopologyNodeDto(
    Guid ConfigurationItemId,
    string CiNumber,
    string Name,
    string CiTypeKey,
    string CiTypeName,
    string Status,
    string? Criticality,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    Guid? LocationId,
    double? PositionX,
    double? PositionY,
    IReadOnlyList<CiNetworkIdentityDto> Identities);

public sealed record TopologyEdgeDto(
    Guid RelationshipId,
    Guid SourceCiId,
    Guid TargetCiId,
    string RelationshipType,
    string? FromPort,
    string? ToPort,
    string? MediaType,
    string? LinkMode,
    string? Notes,
    bool IsConfirmed);

public sealed record TopologyUnmappedDeviceDto(
    Guid ConfigurationItemId,
    string CiNumber,
    string Name,
    string CiTypeKey,
    string CiTypeName,
    string? Manufacturer,
    string? Model,
    string? SerialNumber);

public sealed record NetworkTopologyGraphDto(
    NetworkTopologyViewDto View,
    IReadOnlyList<TopologyNodeDto> Nodes,
    IReadOnlyList<TopologyEdgeDto> Edges,
    IReadOnlyList<TopologyUnmappedDeviceDto> Unmapped);

public sealed record CiNetworkIdentityDto(
    Guid Id,
    Guid ConfigurationItemId,
    string IpAddress,
    string? Hostname,
    string? MacAddress,
    bool IsPrimary);

public sealed record NodeLayoutItem(Guid ConfigurationItemId, double PositionX, double PositionY);

public sealed record NetworkLinkDetailDto(
    Guid Id,
    Guid RelationshipId,
    string? FromPort,
    string? ToPort,
    string? MediaType,
    string? LinkMode,
    string? Notes,
    bool IsConfirmed,
    DateTimeOffset UpdatedAtUtc);

public sealed class NetworkTopologyService(
    CmdbDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit)
{
    public static readonly string[] NetworkRelevantTypeKeys =
    [
        "network-device",
        "firewall",
        "network-link",
        "server",
    ];

    public async Task EnsureNetworkCiTypesExistAsync(CancellationToken cancellationToken = default)
    {
        (string Key, string Name, string Description)[] required =
        [
            ("network-device", "Network Device", "Switches, routers, and other network equipment"),
            ("firewall", "Firewall", "Network firewall appliances"),
            ("network-link", "Network Link", "External or WAN network links and circuits"),
        ];

        HashSet<string> existing = (await db.CiTypes.AsNoTracking()
            .Where(item => required.Select(r => r.Key).Contains(item.Key))
            .Select(item => item.Key)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach ((string key, string name, string description) in required)
        {
            if (existing.Contains(key))
            {
                continue;
            }

            db.CiTypes.Add(CiType.Create(key, name, clock.UtcNow, description));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<NetworkTopologyViewDto> GetOrCreateDefaultViewAsync(
        CancellationToken cancellationToken = default)
    {
        NetworkTopologyView? view = await db.NetworkTopologyViews
            .FirstOrDefaultAsync(item => item.IsDefault, cancellationToken);
        if (view is null)
        {
            view = await db.NetworkTopologyViews
                .FirstOrDefaultAsync(item => item.Name == "Default", cancellationToken);
        }

        if (view is null)
        {
            view = NetworkTopologyView.Create("Default", clock.UtcNow, isDefault: true);
            db.NetworkTopologyViews.Add(view);
            await db.SaveChangesAsync(cancellationToken);
            await businessAudit.AppendAsync(
                CmdbAudit.Created(view.Id, view.Name, "NetworkTopologyCreated"),
                cancellationToken);
        }
        else if (!view.IsDefault)
        {
            view.Update(view.Name, view.LocationId, view.Description, isDefault: true, clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }

        return MapView(view);
    }

    public async Task<IReadOnlyList<NetworkTopologyViewDto>> ListViewsAsync(
        CancellationToken cancellationToken = default)
    {
        await GetOrCreateDefaultViewAsync(cancellationToken);
        return await db.NetworkTopologyViews.AsNoTracking()
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Name)
            .Select(item => new NetworkTopologyViewDto(
                item.Id,
                item.Name,
                item.LocationId,
                item.Description,
                item.IsDefault,
                Convert.ToBase64String(item.RowVersion),
                item.CreatedAtUtc,
                item.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<NetworkTopologyViewDto> CreateViewAsync(
        string name,
        Guid? locationId,
        string? description,
        bool isDefault,
        CancellationToken cancellationToken = default)
    {
        if (isDefault)
        {
            await ClearDefaultFlagsAsync(cancellationToken);
        }

        NetworkTopologyView view = NetworkTopologyView.Create(name, clock.UtcNow, locationId, description, isDefault);
        db.NetworkTopologyViews.Add(view);
        await db.SaveChangesAsync(cancellationToken);
        await businessAudit.AppendAsync(
            CmdbAudit.Created(view.Id, view.Name, "NetworkTopologyCreated"),
            cancellationToken);
        return MapView(view);
    }

    public async Task<NetworkTopologyGraphDto> ListTopologyGraphAsync(
        Guid? viewId = null,
        Guid? locationId = null,
        string? typeKey = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        NetworkTopologyViewDto viewDto;
        if (viewId is null || viewId == Guid.Empty)
        {
            viewDto = await GetOrCreateDefaultViewAsync(cancellationToken);
        }
        else
        {
            NetworkTopologyView view = await db.NetworkTopologyViews
                .FirstOrDefaultAsync(item => item.Id == viewId, cancellationToken)
                ?? throw new InvalidOperationException("Topology view was not found.");
            viewDto = MapView(view);
        }

        Dictionary<Guid, string> typeNames = await db.CiTypes.AsNoTracking()
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        Dictionary<Guid, string> typeKeys = await db.CiTypes.AsNoTracking()
            .ToDictionaryAsync(item => item.Id, item => item.Key, cancellationToken);

        HashSet<string> relevantKeys = NetworkRelevantTypeKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<Guid> relevantTypeIds = typeKeys
            .Where(pair => relevantKeys.Contains(pair.Value))
            .Select(pair => pair.Key)
            .ToList();

        List<CiRelationship> connects = await db.CiRelationships.AsNoTracking()
            .Where(item => item.RelationshipType == CiRelationshipType.ConnectsTo)
            .ToListAsync(cancellationToken);

        HashSet<Guid> connectedCiIds = connects
            .SelectMany(item => new[] { item.SourceCiId, item.TargetCiId })
            .ToHashSet();

        List<NetworkTopologyNodeLayout> layouts = await db.NetworkTopologyNodeLayouts.AsNoTracking()
            .Where(item => item.TopologyViewId == viewDto.Id)
            .ToListAsync(cancellationToken);
        Dictionary<Guid, NetworkTopologyNodeLayout> layoutByCi = layouts
            .ToDictionary(item => item.ConfigurationItemId);

        IQueryable<ConfigurationItem> ciQuery = db.ConfigurationItems.AsNoTracking()
            .Where(item => item.Status != ConfigurationItemStatus.Retired);

        if (locationId is not null && locationId != Guid.Empty)
        {
            ciQuery = ciQuery.Where(item => item.LocationId == locationId);
        }

        if (!string.IsNullOrWhiteSpace(typeKey))
        {
            string key = typeKey.Trim();
            Guid resolvedTypeId = typeKeys
                .Where(pair => string.Equals(pair.Value, key, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Key)
                .FirstOrDefault();
            ciQuery = resolvedTypeId == Guid.Empty
                ? ciQuery.Where(_ => false)
                : ciQuery.Where(item => item.CiTypeId == resolvedTypeId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            ciQuery = ciQuery.Where(item =>
                item.Name.Contains(term)
                || item.CiNumber.Contains(term)
                || (item.SerialNumber != null && item.SerialNumber.Contains(term))
                || (item.Manufacturer != null && item.Manufacturer.Contains(term))
                || (item.Model != null && item.Model.Contains(term)));
        }

        List<ConfigurationItem> candidates = await ciQuery.ToListAsync(cancellationToken);
        List<ConfigurationItem> graphCis = candidates
            .Where(item =>
                relevantTypeIds.Contains(item.CiTypeId)
                || connectedCiIds.Contains(item.Id)
                || layoutByCi.ContainsKey(item.Id))
            .OrderBy(item => item.Name)
            .ToList();

        HashSet<Guid> graphIds = graphCis.Select(item => item.Id).ToHashSet();
        List<Guid> identityCiIds = graphCis.Select(item => item.Id).ToList();
        Dictionary<Guid, List<CiNetworkIdentity>> identities = (await db.CiNetworkIdentities.AsNoTracking()
                .Where(item => identityCiIds.Contains(item.ConfigurationItemId))
                .ToListAsync(cancellationToken))
            .GroupBy(item => item.ConfigurationItemId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.IsPrimary).ToList());

        List<TopologyNodeDto> nodes = graphCis.Select(item =>
        {
            layoutByCi.TryGetValue(item.Id, out NetworkTopologyNodeLayout? layout);
            identities.TryGetValue(item.Id, out List<CiNetworkIdentity>? idents);
            typeKeys.TryGetValue(item.CiTypeId, out string? key);
            typeNames.TryGetValue(item.CiTypeId, out string? typeName);
            return new TopologyNodeDto(
                item.Id,
                item.CiNumber,
                item.Name,
                key ?? string.Empty,
                typeName ?? string.Empty,
                item.Status.ToString(),
                item.Criticality?.ToString(),
                item.Manufacturer,
                item.Model,
                item.SerialNumber,
                item.LocationId,
                layout?.PositionX,
                layout?.PositionY,
                (idents ?? []).Select(MapIdentity).ToList());
        }).ToList();

        Dictionary<Guid, NetworkLinkDetail> linkByRel = await db.NetworkLinkDetails.AsNoTracking()
            .Where(item => connects.Select(c => c.Id).Contains(item.RelationshipId))
            .ToDictionaryAsync(item => item.RelationshipId, cancellationToken);

        List<TopologyEdgeDto> edges = connects
            .Where(item => graphIds.Contains(item.SourceCiId) && graphIds.Contains(item.TargetCiId))
            .Select(item =>
            {
                linkByRel.TryGetValue(item.Id, out NetworkLinkDetail? detail);
                return new TopologyEdgeDto(
                    item.Id,
                    item.SourceCiId,
                    item.TargetCiId,
                    item.RelationshipType.ToString(),
                    detail?.FromPort,
                    detail?.ToPort,
                    detail?.MediaType,
                    detail?.LinkMode,
                    detail?.Notes ?? item.Notes,
                    detail?.IsConfirmed ?? true);
            })
            .ToList();

        List<TopologyUnmappedDeviceDto> unmapped = graphCis
            .Where(item =>
                relevantTypeIds.Contains(item.CiTypeId)
                && !layoutByCi.ContainsKey(item.Id)
                && !connectedCiIds.Contains(item.Id))
            .Select(item =>
            {
                typeKeys.TryGetValue(item.CiTypeId, out string? key);
                typeNames.TryGetValue(item.CiTypeId, out string? typeName);
                return new TopologyUnmappedDeviceDto(
                    item.Id,
                    item.CiNumber,
                    item.Name,
                    key ?? string.Empty,
                    typeName ?? string.Empty,
                    item.Manufacturer,
                    item.Model,
                    item.SerialNumber);
            })
            .OrderBy(item => item.Name)
            .ToList();

        return new NetworkTopologyGraphDto(viewDto, nodes, edges, unmapped);
    }

    public async Task SaveNodeLayoutsAsync(
        Guid viewId,
        IReadOnlyList<NodeLayoutItem> layouts,
        CancellationToken cancellationToken = default)
    {
        NetworkTopologyView view = await db.NetworkTopologyViews
            .FirstOrDefaultAsync(item => item.Id == viewId, cancellationToken)
            ?? throw new InvalidOperationException("Topology view was not found.");

        List<NetworkTopologyNodeLayout> existing = await db.NetworkTopologyNodeLayouts
            .Where(item => item.TopologyViewId == viewId)
            .ToListAsync(cancellationToken);
        Dictionary<Guid, NetworkTopologyNodeLayout> byCi = existing
            .ToDictionary(item => item.ConfigurationItemId);

        foreach (NodeLayoutItem item in layouts)
        {
            if (item.ConfigurationItemId == Guid.Empty)
            {
                continue;
            }

            if (byCi.TryGetValue(item.ConfigurationItemId, out NetworkTopologyNodeLayout? layout))
            {
                layout.SetPosition(item.PositionX, item.PositionY, clock.UtcNow);
            }
            else
            {
                bool ciExists = await db.ConfigurationItems.AsNoTracking()
                    .AnyAsync(ci => ci.Id == item.ConfigurationItemId, cancellationToken);
                if (!ciExists)
                {
                    throw new InvalidOperationException($"Configuration item {item.ConfigurationItemId} was not found.");
                }

                NetworkTopologyNodeLayout created = NetworkTopologyNodeLayout.Create(
                    viewId,
                    item.ConfigurationItemId,
                    item.PositionX,
                    item.PositionY,
                    clock.UtcNow);
                db.NetworkTopologyNodeLayouts.Add(created);
            }
        }

        view.Update(view.Name, view.LocationId, view.Description, view.IsDefault, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await businessAudit.AppendAsync(
            CmdbAudit.Updated(view.Id, view.Name, "NetworkNodePositionUpdated", null, $"{layouts.Count} layout(s)"),
            cancellationToken);
    }

    public async Task<NetworkLinkDetailDto> ConnectAsync(
        Guid sourceCiId,
        Guid targetCiId,
        string? fromPort,
        string? toPort,
        string? mediaType,
        string? linkMode,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (sourceCiId == targetCiId)
        {
            throw new InvalidOperationException("A configuration item cannot link to itself.");
        }

        bool sourceExists = await db.ConfigurationItems.AsNoTracking()
            .AnyAsync(item => item.Id == sourceCiId, cancellationToken);
        bool targetExists = await db.ConfigurationItems.AsNoTracking()
            .AnyAsync(item => item.Id == targetCiId, cancellationToken);
        if (!sourceExists || !targetExists)
        {
            throw new InvalidOperationException("Source or target configuration item was not found.");
        }

        CiRelationship? existing = await db.CiRelationships
            .FirstOrDefaultAsync(
                item => item.SourceCiId == sourceCiId
                    && item.TargetCiId == targetCiId
                    && item.RelationshipType == CiRelationshipType.ConnectsTo,
                cancellationToken);

        CiRelationship relationship = existing ?? CiRelationship.Create(
            sourceCiId,
            targetCiId,
            CiRelationshipType.ConnectsTo,
            clock.UtcNow,
            notes);
        if (existing is null)
        {
            db.CiRelationships.Add(relationship);
        }

        NetworkLinkDetail? detail = await db.NetworkLinkDetails
            .FirstOrDefaultAsync(item => item.RelationshipId == relationship.Id, cancellationToken);
        if (detail is null)
        {
            detail = NetworkLinkDetail.Create(
                relationship.Id,
                clock.UtcNow,
                fromPort,
                toPort,
                mediaType,
                linkMode,
                notes,
                isConfirmed: true);
            db.NetworkLinkDetails.Add(detail);
            await businessAudit.AppendAsync(
                CmdbAudit.Linked(relationship.Id, null, "NetworkRelationshipCreated"),
                cancellationToken);
        }
        else
        {
            detail.Update(fromPort, toPort, mediaType, linkMode, notes, isConfirmed: true, clock.UtcNow);
            await businessAudit.AppendAsync(
                CmdbAudit.Updated(relationship.Id, null, "NetworkRelationshipUpdated"),
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return MapLink(detail);
    }

    public async Task<NetworkLinkDetailDto?> UpdateLinkDetailAsync(
        Guid relationshipId,
        string? fromPort,
        string? toPort,
        string? mediaType,
        string? linkMode,
        string? notes,
        bool isConfirmed,
        CancellationToken cancellationToken = default)
    {
        CiRelationship? relationship = await db.CiRelationships
            .FirstOrDefaultAsync(item => item.Id == relationshipId, cancellationToken);
        if (relationship is null)
        {
            return null;
        }

        NetworkLinkDetail? detail = await db.NetworkLinkDetails
            .FirstOrDefaultAsync(item => item.RelationshipId == relationshipId, cancellationToken);
        if (detail is null)
        {
            detail = NetworkLinkDetail.Create(
                relationshipId,
                clock.UtcNow,
                fromPort,
                toPort,
                mediaType,
                linkMode,
                notes,
                isConfirmed);
            db.NetworkLinkDetails.Add(detail);
        }
        else
        {
            detail.Update(fromPort, toPort, mediaType, linkMode, notes, isConfirmed, clock.UtcNow);
        }

        await db.SaveChangesAsync(cancellationToken);
        await businessAudit.AppendAsync(
            CmdbAudit.Updated(relationshipId, null, "NetworkRelationshipUpdated"),
            cancellationToken);
        return MapLink(detail);
    }

    public async Task<bool> DeleteConnectionAsync(Guid relationshipId, CancellationToken cancellationToken = default)
    {
        CiRelationship? relationship = await db.CiRelationships
            .FirstOrDefaultAsync(item => item.Id == relationshipId, cancellationToken);
        if (relationship is null)
        {
            return false;
        }

        NetworkLinkDetail? detail = await db.NetworkLinkDetails
            .FirstOrDefaultAsync(item => item.RelationshipId == relationshipId, cancellationToken);
        if (detail is not null)
        {
            db.NetworkLinkDetails.Remove(detail);
        }

        db.CiRelationships.Remove(relationship);
        await db.SaveChangesAsync(cancellationToken);
        await businessAudit.AppendAsync(
            CmdbAudit.Unlinked(relationshipId, null, "NetworkRelationshipRemoved"),
            cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<CiNetworkIdentityDto>> ListIdentitiesAsync(
        Guid configurationItemId,
        CancellationToken cancellationToken = default)
    {
        return await db.CiNetworkIdentities.AsNoTracking()
            .Where(item => item.ConfigurationItemId == configurationItemId)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.IpAddress)
            .Select(item => new CiNetworkIdentityDto(
                item.Id,
                item.ConfigurationItemId,
                item.IpAddress,
                item.Hostname,
                item.MacAddress,
                item.IsPrimary))
            .ToListAsync(cancellationToken);
    }

    private async Task ClearDefaultFlagsAsync(CancellationToken cancellationToken)
    {
        List<NetworkTopologyView> defaults = await db.NetworkTopologyViews
            .Where(item => item.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (NetworkTopologyView item in defaults)
        {
            item.Update(item.Name, item.LocationId, item.Description, isDefault: false, clock.UtcNow);
        }
    }

    private static NetworkTopologyViewDto MapView(NetworkTopologyView view) =>
        new(
            view.Id,
            view.Name,
            view.LocationId,
            view.Description,
            view.IsDefault,
            Convert.ToBase64String(view.RowVersion),
            view.CreatedAtUtc,
            view.UpdatedAtUtc);

    private static CiNetworkIdentityDto MapIdentity(CiNetworkIdentity item) =>
        new(item.Id, item.ConfigurationItemId, item.IpAddress, item.Hostname, item.MacAddress, item.IsPrimary);

    private static NetworkLinkDetailDto MapLink(NetworkLinkDetail detail) =>
        new(
            detail.Id,
            detail.RelationshipId,
            detail.FromPort,
            detail.ToPort,
            detail.MediaType,
            detail.LinkMode,
            detail.Notes,
            detail.IsConfirmed,
            detail.UpdatedAtUtc);
}

internal static class CmdbAudit
{
    public static BusinessAuditEntry Created(Guid id, string? number, string field) => new()
    {
        AggregateType = AuditAggregateType.Cmdb,
        AggregateId = id,
        BusinessNumber = number,
        Action = BusinessAuditAction.Created,
        FieldName = field,
        Source = AuditSource.Api,
    };

    public static BusinessAuditEntry Updated(
        Guid id,
        string? number,
        string field,
        string? oldValue = null,
        string? newValue = null) => new()
    {
        AggregateType = AuditAggregateType.Cmdb,
        AggregateId = id,
        BusinessNumber = number,
        Action = BusinessAuditAction.Updated,
        FieldName = field,
        OldValue = oldValue,
        NewValue = newValue,
        Source = AuditSource.Api,
    };

    public static BusinessAuditEntry Linked(Guid id, string? number, string field) => new()
    {
        AggregateType = AuditAggregateType.Cmdb,
        AggregateId = id,
        BusinessNumber = number,
        Action = BusinessAuditAction.Linked,
        FieldName = field,
        Source = AuditSource.Api,
    };

    public static BusinessAuditEntry Unlinked(Guid id, string? number, string field) => new()
    {
        AggregateType = AuditAggregateType.Cmdb,
        AggregateId = id,
        BusinessNumber = number,
        Action = BusinessAuditAction.Unlinked,
        FieldName = field,
        Source = AuditSource.Api,
    };
}
