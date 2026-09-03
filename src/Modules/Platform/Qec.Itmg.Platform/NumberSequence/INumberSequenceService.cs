using Qec.Itmg.BuildingBlocks.Time;

namespace Qec.Itmg.Platform.NumberSequence;

public interface INumberSequenceService
{
    /// <summary>
    /// Issues the next business number for <paramref name="sequenceKey"/> in the current UTC year.
    /// Format: PREFIX-YYYY-000001
    /// </summary>
    Task<string> NextAsync(
        string sequenceKey,
        string prefix,
        CancellationToken cancellationToken = default);
}

