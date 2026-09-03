namespace Qec.Itmg.BuildingBlocks.Time;

/// <summary>
/// UTC-oriented clock abstraction for testability. Persist and compare instants in UTC.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
