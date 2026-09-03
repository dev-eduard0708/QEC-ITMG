namespace Qec.Itmg.Cmdb.Domain;

public sealed class BusinessServiceConfigurationItem
{
    private BusinessServiceConfigurationItem()
    {
    }

    public Guid BusinessServiceId { get; private set; }

    public Guid ConfigurationItemId { get; private set; }

    public DateTimeOffset LinkedAtUtc { get; private set; }

    public static BusinessServiceConfigurationItem Create(
        Guid businessServiceId,
        Guid configurationItemId,
        DateTimeOffset utcNow)
    {
        if (businessServiceId == Guid.Empty)
        {
            throw new ArgumentException("Business service is required.", nameof(businessServiceId));
        }

        if (configurationItemId == Guid.Empty)
        {
            throw new ArgumentException("Configuration item is required.", nameof(configurationItemId));
        }

        return new BusinessServiceConfigurationItem
        {
            BusinessServiceId = businessServiceId,
            ConfigurationItemId = configurationItemId,
            LinkedAtUtc = utcNow,
        };
    }
}
