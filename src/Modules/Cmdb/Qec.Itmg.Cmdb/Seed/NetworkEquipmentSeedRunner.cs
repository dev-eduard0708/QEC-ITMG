using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb.Domain;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Contracts.Numbering;

namespace Qec.Itmg.Cmdb.Seed;

public interface INetworkEquipmentSeedRunner
{
    Task RunAsync(CancellationToken cancellationToken = default);
}

public sealed class NetworkEquipmentSeedRunner(
    CmdbDbContext db,
    INumberSequenceService numbers,
    IClock clock,
    ILogger<NetworkEquipmentSeedRunner> logger) : INetworkEquipmentSeedRunner
{
    private static readonly (string Key, string Name, string Description)[] NetworkTypes =
    [
        ("network-device", "Network Device", "Switches, routers, and other network equipment"),
        ("firewall", "Firewall", "Network firewall appliances"),
        ("network-link", "Network Link", "External or WAN network links and circuits"),
    ];

    private static readonly SeedDevice[] Devices =
    [
        new("Catalyst 2960-X #1", "Cisco", "Catalyst 2960-X", "FCW2049B6H9", "network-device", null, null),
        new("Catalyst 2960-X #2", "Cisco", "Catalyst 2960-X", "FWC1837A2ML", "network-device", null, null),
        new("Catalyst 2960-X #3", "Cisco", "Catalyst 2960-X", "FWC1837A2PQ", "network-device", null, null),
        new("Catalyst 2960-X #4", "Cisco", "Catalyst 2960-X", "FWC1837A2RF", "network-device", null, null),
        new("Core Switch", "Cisco", "Catalyst 3750-X Series", "FD01906F27Q", "network-device", null, "Role: Core Switch"),
        new("Farm Switch", "Cisco", "Catalyst 2960-S Series", "FOC1942W4PW", "network-device", null, null),
        new("SonicWall Firewall", "SonicWall", "NSA 2700", null, "firewall", "SEED:SONICWALL_NSA2700", null),
        new("Zain Internet", "Zain", null, null, "network-link", "SEED:ZAIN_INTERNET", null),
    ];

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<string, Guid> typeIds = await EnsureTypesAsync(cancellationToken);
        int added = 0;

        foreach (SeedDevice device in Devices)
        {
            if (!typeIds.TryGetValue(device.TypeKey, out Guid typeId))
            {
                continue;
            }

            if (await ExistsAsync(device, cancellationToken))
            {
                continue;
            }

            string ciNumber = await numbers.NextAsync(
                ConfigurationItemService.CiSequenceKey,
                ConfigurationItemService.CiNumberPrefix,
                cancellationToken);
            string? notes = device.SeedKey is null
                ? device.Notes
                : device.Notes is null
                    ? device.SeedKey
                    : $"{device.SeedKey}; {device.Notes}";

            db.ConfigurationItems.Add(ConfigurationItem.Create(
                ciNumber,
                typeId,
                device.Name,
                clock.UtcNow,
                description: null,
                criticality: null,
                locationId: null,
                departmentId: null,
                ownerUserId: null,
                serialNumber: device.SerialNumber,
                manufacturer: device.Manufacturer,
                model: device.Model,
                notes: notes));
            added++;
        }

        NetworkTopologyView? defaultView = await db.NetworkTopologyViews
            .FirstOrDefaultAsync(item => item.IsDefault || item.Name == "Default", cancellationToken);
        bool viewChanged = false;
        if (defaultView is null)
        {
            db.NetworkTopologyViews.Add(
                NetworkTopologyView.Create("Default", clock.UtcNow, isDefault: true));
            viewChanged = true;
        }
        else if (!defaultView.IsDefault)
        {
            defaultView.Update(
                defaultView.Name,
                defaultView.LocationId,
                defaultView.Description,
                isDefault: true,
                clock.UtcNow);
            viewChanged = true;
        }

        if (added > 0 || viewChanged)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Network equipment seed completed. Added {AddedCount} CI(s); ensured Default topology view.",
            added);
    }

    private async Task<Dictionary<string, Guid>> EnsureTypesAsync(CancellationToken cancellationToken)
    {
        string[] keys = NetworkTypes.Select(static item => item.Key).ToArray();
        Dictionary<string, Guid> existing = await db.CiTypes
            .Where(item => keys.Contains(item.Key))
            .ToDictionaryAsync(item => item.Key, item => item.Id, cancellationToken);

        foreach ((string key, string name, string description) in NetworkTypes)
        {
            if (existing.ContainsKey(key))
            {
                continue;
            }

            CiType type = CiType.Create(key, name, clock.UtcNow, description);
            db.CiTypes.Add(type);
            existing[key] = type.Id;
        }

        if (existing.Count < NetworkTypes.Length || db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
            existing = await db.CiTypes
                .Where(item => keys.Contains(item.Key))
                .ToDictionaryAsync(item => item.Key, item => item.Id, cancellationToken);
        }

        return existing;
    }

    private async Task<bool> ExistsAsync(SeedDevice device, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(device.SerialNumber))
        {
            return await db.ConfigurationItems.AsNoTracking()
                .AnyAsync(item => item.SerialNumber == device.SerialNumber, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(device.SeedKey))
        {
            string key = device.SeedKey;
            return await db.ConfigurationItems.AsNoTracking()
                .AnyAsync(item => item.Notes != null && item.Notes.Contains(key), cancellationToken);
        }

        return await db.ConfigurationItems.AsNoTracking()
            .AnyAsync(item => item.Name == device.Name, cancellationToken);
    }

    private sealed record SeedDevice(
        string Name,
        string Manufacturer,
        string? Model,
        string? SerialNumber,
        string TypeKey,
        string? SeedKey,
        string? Notes);
}
