using System.Threading.Channels;

namespace Qec.Itmg.Cmdb.Discovery;

public sealed record NetworkDiscoveryRunWorkItem(Guid RunId);

public interface INetworkDiscoveryRunQueue
{
    ValueTask EnqueueAsync(NetworkDiscoveryRunWorkItem item, CancellationToken cancellationToken = default);

    IAsyncEnumerable<NetworkDiscoveryRunWorkItem> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class NetworkDiscoveryRunQueue : INetworkDiscoveryRunQueue
{
    private readonly Channel<NetworkDiscoveryRunWorkItem> _channel =
        Channel.CreateUnbounded<NetworkDiscoveryRunWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    public ValueTask EnqueueAsync(NetworkDiscoveryRunWorkItem item, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(item, cancellationToken);

    public IAsyncEnumerable<NetworkDiscoveryRunWorkItem> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
