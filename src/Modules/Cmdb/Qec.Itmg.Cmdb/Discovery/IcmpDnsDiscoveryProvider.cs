using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Qec.Itmg.Cmdb.Discovery;

public sealed record NetworkDiscoveryScanRequest(
    string Cidr,
    int TimeoutMs,
    int MaxConcurrency,
    bool AllowNonPrivate = false);

public sealed record NetworkHostObservation(
    string IpAddress,
    string? Hostname,
    string? MacAddress,
    string? Vendor,
    int? ResponseMs);

public interface INetworkDiscoveryProvider
{
    Task<IReadOnlyList<NetworkHostObservation>> DiscoverAsync(
        NetworkDiscoveryScanRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// V1 provider: ICMP reachability + reverse DNS. No credentials, ports, or SNMP.
/// Extension point for future SNMP/LLDP/CDP/MAC-table providers.
/// </summary>
public sealed class IcmpDnsDiscoveryProvider : INetworkDiscoveryProvider
{
    public const int MaxAddressesPerRun = 1024;

    public async Task<IReadOnlyList<NetworkHostObservation>> DiscoverAsync(
        NetworkDiscoveryScanRequest request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IPAddress> addresses = ExpandCidr(request.Cidr, request.AllowNonPrivate);
        int concurrency = Math.Clamp(request.MaxConcurrency, 1, 32);
        int timeoutMs = Math.Clamp(request.TimeoutMs, 100, 30_000);

        using var gate = new SemaphoreSlim(concurrency, concurrency);
        var results = new List<NetworkHostObservation>();
        var sync = new object();

        IEnumerable<Task> tasks = addresses.Select(async address =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                NetworkHostObservation? observation = await ProbeAsync(address, timeoutMs, cancellationToken)
                    .ConfigureAwait(false);
                if (observation is null)
                {
                    return;
                }

                lock (sync)
                {
                    results.Add(observation);
                }
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return results
            .OrderBy(item => item.IpAddress, StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<IPAddress> ExpandCidr(string cidr, bool allowNonPrivate = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cidr);
        string[] parts = cidr.Trim().Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out IPAddress? network)
            || network.AddressFamily != AddressFamily.InterNetwork
            || !int.TryParse(parts[1], out int prefix)
            || prefix < 0
            || prefix > 32)
        {
            throw new ArgumentException("A valid IPv4 CIDR is required (for example 192.168.1.0/24).", nameof(cidr));
        }

        if (!allowNonPrivate && !IsRfc1918(network))
        {
            throw new InvalidOperationException(
                "V1 discovery only allows RFC1918 private IPv4 ranges unless explicitly allow-listed.");
        }

        uint networkValue = ToUInt32(network);
        int hostBits = 32 - prefix;
        long hostCount = hostBits >= 31 ? 1L << Math.Min(hostBits, 31) : 1L << hostBits;
        // Exclude network and broadcast for prefix < 31 when space permits.
        long usable = prefix >= 31 ? hostCount : Math.Max(0, hostCount - 2);
        if (usable > MaxAddressesPerRun)
        {
            throw new InvalidOperationException(
                $"CIDR expands to more than {MaxAddressesPerRun} addresses. Narrow the range.");
        }

        uint mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        uint baseNetwork = networkValue & mask;
        var addresses = new List<IPAddress>((int)usable);

        if (prefix >= 31)
        {
            for (uint offset = 0; offset < hostCount; offset++)
            {
                addresses.Add(FromUInt32(baseNetwork + offset));
            }
        }
        else
        {
            for (uint offset = 1; offset < hostCount - 1; offset++)
            {
                addresses.Add(FromUInt32(baseNetwork + offset));
            }
        }

        return addresses;
    }

    private static async Task<NetworkHostObservation?> ProbeAsync(
        IPAddress address,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        using var ping = new Ping();
        try
        {
            PingReply reply = await ping.SendPingAsync(address, timeoutMs).WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            if (reply.Status != IPStatus.Success)
            {
                return null;
            }

            string? hostname = null;
            try
            {
                IPHostEntry entry = await Dns.GetHostEntryAsync(address.ToString(), cancellationToken)
                    .ConfigureAwait(false);
                hostname = string.IsNullOrWhiteSpace(entry.HostName) ? null : entry.HostName;
            }
            catch (SocketException)
            {
                // Reverse DNS is best-effort.
            }
            catch (ArgumentException)
            {
            }

            return new NetworkHostObservation(
                address.ToString(),
                hostname,
                MacAddress: null,
                Vendor: null,
                ResponseMs: (int)Math.Clamp(reply.RoundtripTime, 0, int.MaxValue));
        }
        catch (PingException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsRfc1918(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        if (bytes.Length != 4)
        {
            return false;
        }

        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }

    private static uint ToUInt32(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return BitConverter.ToUInt32(bytes, 0);
    }

    private static IPAddress FromUInt32(uint value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return new IPAddress(bytes);
    }
}
