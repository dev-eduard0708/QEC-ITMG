namespace Qec.Itmg.ServiceDesk.Domain;

public enum ProblemStatus
{
    New = 0,
    Investigating = 1,
    Resolved = 2,
    Closed = 3,
}

public sealed class Problem
{
    private Problem()
    {
    }

    public Guid Id { get; private set; }

    public string ProblemNumber { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public ProblemStatus Status { get; private set; }

    public TicketPriority Priority { get; private set; }

    public Guid? OwnerUserId { get; private set; }

    public Guid? ConfigurationItemId { get; private set; }

    public string? RootCause { get; private set; }

    public string? Workaround { get; private set; }

    public bool IsKnownError { get; private set; }

    public DateTimeOffset? KnownErrorAtUtc { get; private set; }

    public Guid? KnownErrorByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static Problem Create(
        string problemNumber,
        string title,
        string description,
        TicketPriority priority,
        DateTimeOffset utcNow,
        Guid? ownerUserId = null,
        Guid? configurationItemId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(problemNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }

        return new Problem
        {
            Id = Guid.CreateVersion7(),
            ProblemNumber = problemNumber.Trim(),
            Title = title.Trim(),
            Description = description.Trim(),
            Status = ProblemStatus.New,
            Priority = priority,
            OwnerUserId = NormalizeGuid(ownerUserId),
            ConfigurationItemId = NormalizeGuid(configurationItemId),
            IsKnownError = false,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    /// <summary>Marks or clears Known Error classification (P5-07).</summary>
    public void SetKnownError(bool isKnownError, Guid byUserId, string rowVersion, DateTimeOffset utcNow)
    {
        if (byUserId == Guid.Empty)
        {
            throw new ArgumentException("User is required.", nameof(byUserId));
        }

        EnsureRowVersion(rowVersion);

        if (isKnownError)
        {
            IsKnownError = true;
            KnownErrorAtUtc ??= utcNow;
            KnownErrorByUserId ??= byUserId;
        }
        else
        {
            IsKnownError = false;
            KnownErrorAtUtc = null;
            KnownErrorByUserId = null;
        }

        UpdatedAtUtc = utcNow;
    }

    public void UpdateDetails(
        string title,
        string description,
        TicketPriority priority,
        Guid? ownerUserId,
        Guid? configurationItemId,
        string? rootCause,
        string? workaround,
        string rowVersion,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }

        EnsureRowVersion(rowVersion);

        Title = title.Trim();
        Description = description.Trim();
        Priority = priority;
        OwnerUserId = NormalizeGuid(ownerUserId);
        ConfigurationItemId = NormalizeGuid(configurationItemId);
        RootCause = NormalizeOptional(rootCause);
        Workaround = NormalizeOptional(workaround);
        UpdatedAtUtc = utcNow;
    }

    public void ChangeStatus(ProblemStatus target, DateTimeOffset utcNow, string? rowVersion = null)
    {
        if (!Enum.IsDefined(target))
        {
            throw new ArgumentOutOfRangeException(nameof(target));
        }

        if (rowVersion is not null)
        {
            EnsureRowVersion(rowVersion);
        }

        if (!IsTransitionAllowed(Status, target))
        {
            throw new InvalidOperationException($"Cannot transition problem from {Status} to {target}.");
        }

        Status = target;
        UpdatedAtUtc = utcNow;

        if (target == ProblemStatus.Resolved)
        {
            ResolvedAtUtc ??= utcNow;
            ClosedAtUtc = null;
        }
        else if (target == ProblemStatus.Closed)
        {
            ResolvedAtUtc ??= utcNow;
            ClosedAtUtc = utcNow;
        }
        else if (target is ProblemStatus.New or ProblemStatus.Investigating)
        {
            ClosedAtUtc = null;
            if (target == ProblemStatus.Investigating && ResolvedAtUtc is not null)
            {
                ResolvedAtUtc = null;
            }
        }
    }

    public static bool IsTransitionAllowed(ProblemStatus from, ProblemStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return from switch
        {
            ProblemStatus.New => to is ProblemStatus.Investigating or ProblemStatus.Closed,
            ProblemStatus.Investigating => to is ProblemStatus.Resolved or ProblemStatus.Closed or ProblemStatus.New,
            ProblemStatus.Resolved => to is ProblemStatus.Closed or ProblemStatus.Investigating,
            ProblemStatus.Closed => to is ProblemStatus.Investigating,
            _ => false,
        };
    }

    private void EnsureRowVersion(string expectedBase64)
    {
        if (!MatchesRowVersion(RowVersion, expectedBase64))
        {
            throw new InvalidOperationException("The problem was modified by another user.");
        }
    }

    private static bool MatchesRowVersion(byte[] current, string expectedBase64)
    {
        if (string.IsNullOrWhiteSpace(expectedBase64))
        {
            return current.Length == 0;
        }

        try
        {
            byte[] expected = Convert.FromBase64String(expectedBase64.Trim());
            return current.AsSpan().SequenceEqual(expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static Guid? NormalizeGuid(Guid? value) =>
        value is null || value == Guid.Empty ? null : value;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
