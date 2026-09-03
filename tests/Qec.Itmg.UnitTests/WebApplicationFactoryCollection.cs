using Xunit;

namespace Qec.Itmg.UnitTests;

/// <summary>
/// Serializes WebApplicationFactory hosts so Serilog / host bootstrap stay stable under CI parallelism.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WebApplicationFactoryCollection : ICollectionFixture<object>
{
    public const string Name = "WebApplicationFactory";
}
