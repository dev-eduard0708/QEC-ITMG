namespace Qec.Itmg.Platform.Domain;

/// <summary>
/// SQL-backed per-year business number generator state.
/// </summary>
public sealed class NumberSequence
{
    public string SequenceKey { get; private set; } = string.Empty;

    public int Year { get; private set; }

    /// <summary>
    /// Next numeric value to issue for this (SequenceKey, Year).
    /// </summary>
    public long NextValue { get; private set; }

    // EF
    private NumberSequence()
    {
    }

    public NumberSequence(string sequenceKey, int year, long nextValue)
    {
        SequenceKey = sequenceKey;
        Year = year;
        NextValue = nextValue;
    }

    /// <summary>
    /// Consumes the current next value and advances to the following value.
    /// </summary>
    public long ConsumeNextValue()
    {
        long issued = NextValue;
        NextValue = checked(issued + 1);
        return issued;
    }
}

