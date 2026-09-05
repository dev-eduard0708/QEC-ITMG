namespace Qec.Itmg.AccessManagement.Domain;

public sealed class AccessReviewCampaign
{
    private AccessReviewCampaign() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public AccessReviewType Type { get; private set; }
    public Guid ReviewerUserId { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset DueAtUtc { get; private set; }
    public AccessReviewCampaignStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static AccessReviewCampaign Create(
        string name,
        AccessReviewType type,
        Guid reviewerUserId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset dueAtUtc,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (reviewerUserId == Guid.Empty) throw new ArgumentException("Reviewer is required.", nameof(reviewerUserId));
        if (dueAtUtc < startsAtUtc) throw new ArgumentException("Due date must be on or after start.", nameof(dueAtUtc));
        return new AccessReviewCampaign
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Type = type,
            ReviewerUserId = reviewerUserId,
            StartsAtUtc = startsAtUtc,
            DueAtUtc = dueAtUtc,
            Status = AccessReviewCampaignStatus.Draft,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Open(DateTimeOffset utcNow)
    {
        if (Status != AccessReviewCampaignStatus.Draft)
            throw new InvalidOperationException("Only draft campaigns can be opened.");
        Status = AccessReviewCampaignStatus.Open;
        UpdatedAtUtc = utcNow;
    }

    public void Complete(DateTimeOffset utcNow)
    {
        if (Status != AccessReviewCampaignStatus.Open)
            throw new InvalidOperationException("Only open campaigns can be completed.");
        Status = AccessReviewCampaignStatus.Completed;
        UpdatedAtUtc = utcNow;
    }
}

public sealed class AccessReviewItem
{
    private AccessReviewItem() { }

    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid? SubjectUserId { get; private set; }
    public Guid? AccountRecordId { get; private set; }
    public Guid? ConfigurationItemId { get; private set; }
    public string AccessSummary { get; private set; } = null!;
    public AccessReviewDecision Decision { get; private set; }
    public string? ReviewerComment { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static AccessReviewItem Create(
        Guid campaignId,
        string accessSummary,
        DateTimeOffset utcNow,
        Guid? subjectUserId = null,
        Guid? accountRecordId = null,
        Guid? configurationItemId = null)
    {
        if (campaignId == Guid.Empty) throw new ArgumentException("Campaign is required.", nameof(campaignId));
        ArgumentException.ThrowIfNullOrWhiteSpace(accessSummary);
        return new AccessReviewItem
        {
            Id = Guid.CreateVersion7(),
            CampaignId = campaignId,
            SubjectUserId = subjectUserId is null || subjectUserId == Guid.Empty ? null : subjectUserId,
            AccountRecordId = accountRecordId is null || accountRecordId == Guid.Empty ? null : accountRecordId,
            ConfigurationItemId = configurationItemId is null || configurationItemId == Guid.Empty ? null : configurationItemId,
            AccessSummary = accessSummary.Trim(),
            Decision = AccessReviewDecision.Pending,
            CreatedAtUtc = utcNow,
        };
    }

    public void Decide(AccessReviewDecision decision, string? comment, DateTimeOffset utcNow)
    {
        if (decision == AccessReviewDecision.Pending)
            throw new ArgumentException("Decision cannot remain Pending.", nameof(decision));
        Decision = decision;
        ReviewerComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        ReviewedAtUtc = utcNow;
    }
}

public sealed class ManagedAccount
{
    private ManagedAccount() { }

    public Guid Id { get; private set; }
    public string AccountName { get; private set; } = null!;
    public ManagedAccountType Type { get; private set; }
    public Guid? ConfigurationItemId { get; private set; }
    public Guid? OwnerUserId { get; private set; }
    public Guid? VendorId { get; private set; }
    public string Purpose { get; private set; } = null!;
    public ManagedAccountStatus Status { get; private set; }
    public DateTimeOffset? LastReviewedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static ManagedAccount Create(
        string accountName,
        ManagedAccountType type,
        string purpose,
        DateTimeOffset utcNow,
        Guid? configurationItemId = null,
        Guid? ownerUserId = null,
        Guid? vendorId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        if (type == ManagedAccountType.Service && (ownerUserId is null || ownerUserId == Guid.Empty))
            throw new ArgumentException("Service accounts require an owner.", nameof(ownerUserId));
        return new ManagedAccount
        {
            Id = Guid.CreateVersion7(),
            AccountName = accountName.Trim(),
            Type = type,
            ConfigurationItemId = configurationItemId is null || configurationItemId == Guid.Empty ? null : configurationItemId,
            OwnerUserId = ownerUserId is null || ownerUserId == Guid.Empty ? null : ownerUserId,
            VendorId = vendorId is null || vendorId == Guid.Empty ? null : vendorId,
            Purpose = purpose.Trim(),
            Status = ManagedAccountStatus.Active,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string accountName,
        string purpose,
        Guid? configurationItemId,
        Guid? ownerUserId,
        ManagedAccountStatus status,
        DateTimeOffset? lastReviewedAtUtc,
        DateTimeOffset utcNow,
        Guid? vendorId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        if (Type == ManagedAccountType.Service && (ownerUserId is null || ownerUserId == Guid.Empty))
            throw new ArgumentException("Service accounts require an owner.", nameof(ownerUserId));
        AccountName = accountName.Trim();
        Purpose = purpose.Trim();
        ConfigurationItemId = configurationItemId is null || configurationItemId == Guid.Empty ? null : configurationItemId;
        OwnerUserId = ownerUserId is null || ownerUserId == Guid.Empty ? null : ownerUserId;
        VendorId = vendorId is null || vendorId == Guid.Empty ? null : vendorId;
        Status = status;
        LastReviewedAtUtc = lastReviewedAtUtc;
        UpdatedAtUtc = utcNow;
    }

    public void SetVendorId(Guid? vendorId, DateTimeOffset utcNow)
    {
        VendorId = vendorId is null || vendorId == Guid.Empty ? null : vendorId;
        UpdatedAtUtc = utcNow;
    }
}

public sealed class SodRule
{
    private SodRule() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public Guid? ApplicationConfigurationItemId { get; private set; }
    public string LeftEntitlementKey { get; private set; } = null!;
    public string RightEntitlementKey { get; private set; } = null!;
    public string Severity { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static SodRule Create(
        string name,
        string leftEntitlementKey,
        string rightEntitlementKey,
        string severity,
        DateTimeOffset utcNow,
        Guid? applicationConfigurationItemId = null,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(leftEntitlementKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(rightEntitlementKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(severity);
        if (string.Equals(leftEntitlementKey.Trim(), rightEntitlementKey.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Left and right entitlements must differ.");
        return new SodRule
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            ApplicationConfigurationItemId = applicationConfigurationItemId is null || applicationConfigurationItemId == Guid.Empty
                ? null : applicationConfigurationItemId,
            LeftEntitlementKey = leftEntitlementKey.Trim(),
            RightEntitlementKey = rightEntitlementKey.Trim(),
            Severity = severity.Trim(),
            IsActive = true,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string name,
        string leftEntitlementKey,
        string rightEntitlementKey,
        string severity,
        bool isActive,
        Guid? applicationConfigurationItemId,
        string? description,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(leftEntitlementKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(rightEntitlementKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(severity);
        Name = name.Trim();
        LeftEntitlementKey = leftEntitlementKey.Trim();
        RightEntitlementKey = rightEntitlementKey.Trim();
        Severity = severity.Trim();
        IsActive = isActive;
        ApplicationConfigurationItemId = applicationConfigurationItemId is null || applicationConfigurationItemId == Guid.Empty
            ? null : applicationConfigurationItemId;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAtUtc = utcNow;
    }
}
