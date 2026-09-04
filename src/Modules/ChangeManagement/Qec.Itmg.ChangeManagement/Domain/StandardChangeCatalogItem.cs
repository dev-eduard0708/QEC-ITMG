namespace Qec.Itmg.ChangeManagement.Domain;

public sealed class StandardChangeCatalogItem
{
    private StandardChangeCatalogItem()
    {
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public ChangeRiskRating RiskRating { get; private set; }
    public string ImplementationPlan { get; private set; } = null!;
    public string TestPlan { get; private set; } = null!;
    public string RollbackPlan { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static StandardChangeCatalogItem Create(
        string code,
        string name,
        ChangeRiskRating riskRating,
        string implementationPlan,
        string testPlan,
        string rollbackPlan,
        DateTimeOffset utcNow,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(implementationPlan);
        ArgumentException.ThrowIfNullOrWhiteSpace(testPlan);
        ArgumentException.ThrowIfNullOrWhiteSpace(rollbackPlan);
        if (!Enum.IsDefined(riskRating)) throw new ArgumentOutOfRangeException(nameof(riskRating));

        return new StandardChangeCatalogItem
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            RiskRating = riskRating,
            ImplementationPlan = implementationPlan.Trim(),
            TestPlan = testPlan.Trim(),
            RollbackPlan = rollbackPlan.Trim(),
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string name,
        string? description,
        ChangeRiskRating riskRating,
        string implementationPlan,
        string testPlan,
        string rollbackPlan,
        bool isActive,
        string rowVersion,
        DateTimeOffset utcNow)
    {
        EnsureRowVersion(rowVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(implementationPlan);
        ArgumentException.ThrowIfNullOrWhiteSpace(testPlan);
        ArgumentException.ThrowIfNullOrWhiteSpace(rollbackPlan);
        if (!Enum.IsDefined(riskRating)) throw new ArgumentOutOfRangeException(nameof(riskRating));

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        RiskRating = riskRating;
        ImplementationPlan = implementationPlan.Trim();
        TestPlan = testPlan.Trim();
        RollbackPlan = rollbackPlan.Trim();
        IsActive = isActive;
        UpdatedAtUtc = utcNow;
    }

    private void EnsureRowVersion(string expectedBase64)
    {
        if (!MatchesRowVersion(RowVersion, expectedBase64))
        {
            throw new InvalidOperationException("The catalog item was modified by another user.");
        }
    }

    private static bool MatchesRowVersion(byte[] current, string expectedBase64)
    {
        if (string.IsNullOrWhiteSpace(expectedBase64)) return current.Length == 0;
        try
        {
            return current.AsSpan().SequenceEqual(Convert.FromBase64String(expectedBase64.Trim()));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
