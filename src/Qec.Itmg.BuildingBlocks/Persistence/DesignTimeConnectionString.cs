namespace Qec.Itmg.BuildingBlocks.Persistence;

/// <summary>
/// Non-secret localhost development connection used by EF design-time factories.
/// Override with environment variable ConnectionStrings__QecItmg when needed.
/// </summary>
public static class DesignTimeConnectionString
{
    public const string DevelopmentDefault =
        "Server=.\\SQLEXPRESS;Database=QecItmg_Dev;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    public static string Resolve()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("ConnectionStrings__QecItmg");
        return string.IsNullOrWhiteSpace(fromEnvironment) ? DevelopmentDefault : fromEnvironment;
    }
}
