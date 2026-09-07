namespace Qec.Itmg.Cmdb.Domain;

public sealed class NetworkTopologyNodeLayout
{
    private NetworkTopologyNodeLayout()
    {
    }

    public Guid Id { get; private set; }

    public Guid TopologyViewId { get; private set; }

    public Guid ConfigurationItemId { get; private set; }

    public double PositionX { get; private set; }

    public double PositionY { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static NetworkTopologyNodeLayout Create(
        Guid topologyViewId,
        Guid configurationItemId,
        double positionX,
        double positionY,
        DateTimeOffset utcNow)
    {
        if (topologyViewId == Guid.Empty)
        {
            throw new ArgumentException("Topology view is required.", nameof(topologyViewId));
        }

        if (configurationItemId == Guid.Empty)
        {
            throw new ArgumentException("Configuration item is required.", nameof(configurationItemId));
        }

        return new NetworkTopologyNodeLayout
        {
            Id = Guid.CreateVersion7(),
            TopologyViewId = topologyViewId,
            ConfigurationItemId = configurationItemId,
            PositionX = positionX,
            PositionY = positionY,
            UpdatedAtUtc = utcNow,
        };
    }

    public void SetPosition(double positionX, double positionY, DateTimeOffset utcNow)
    {
        PositionX = positionX;
        PositionY = positionY;
        UpdatedAtUtc = utcNow;
    }
}
