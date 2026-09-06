namespace Qec.Itmg.AccessManagement.Domain;

public enum AccessRevokeAction
{
    Remove = 0,
    Disable = 1,
}

public enum UserAccessEntitlementStatus
{
    Active = 0,
    Removed = 1,
    Disabled = 2,
}

public sealed class AccessEntitlement
{
    private AccessEntitlement() { }

    public Guid Id { get; private set; }
    public string Key { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public string NameAr { get; private set; } = null!;
    public string? DescriptionEn { get; private set; }
    public string? DescriptionAr { get; private set; }
    public AccessRevokeAction DefaultRevokeAction { get; private set; }
    public bool IsPrivileged { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static AccessEntitlement Create(
        string key,
        string nameEn,
        string nameAr,
        AccessRevokeAction defaultRevokeAction,
        DateTimeOffset utcNow,
        string? descriptionEn = null,
        string? descriptionAr = null,
        bool isPrivileged = false,
        bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        if (!Enum.IsDefined(defaultRevokeAction))
            throw new ArgumentOutOfRangeException(nameof(defaultRevokeAction));
        if (defaultRevokeAction is not (AccessRevokeAction.Remove or AccessRevokeAction.Disable))
            throw new ArgumentOutOfRangeException(nameof(defaultRevokeAction), "Revoke action must be Remove or Disable.");

        return new AccessEntitlement
        {
            Id = Guid.CreateVersion7(),
            Key = key.Trim().ToUpperInvariant(),
            NameEn = nameEn.Trim(),
            NameAr = nameAr.Trim(),
            DescriptionEn = Norm(descriptionEn),
            DescriptionAr = Norm(descriptionAr),
            DefaultRevokeAction = defaultRevokeAction,
            IsPrivileged = isPrivileged,
            IsActive = isActive,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string nameEn,
        string nameAr,
        string? descriptionEn,
        string? descriptionAr,
        AccessRevokeAction defaultRevokeAction,
        bool isPrivileged,
        bool isActive,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        if (!Enum.IsDefined(defaultRevokeAction))
            throw new ArgumentOutOfRangeException(nameof(defaultRevokeAction));
        if (defaultRevokeAction is not (AccessRevokeAction.Remove or AccessRevokeAction.Disable))
            throw new ArgumentOutOfRangeException(nameof(defaultRevokeAction), "Revoke action must be Remove or Disable.");

        NameEn = nameEn.Trim();
        NameAr = nameAr.Trim();
        DescriptionEn = Norm(descriptionEn);
        DescriptionAr = Norm(descriptionAr);
        DefaultRevokeAction = defaultRevokeAction;
        IsPrivileged = isPrivileged;
        IsActive = isActive;
        UpdatedAtUtc = utcNow;
    }

    public AccessItemAction ToItemRevokeAction() =>
        DefaultRevokeAction == AccessRevokeAction.Disable
            ? AccessItemAction.Disable
            : AccessItemAction.Remove;

    private static string? Norm(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class AccessCategoryEntitlement
{
    private AccessCategoryEntitlement() { }

    public Guid Id { get; private set; }
    public Guid AccessCategoryId { get; private set; }
    public Guid AccessEntitlementId { get; private set; }
    public bool IsDefaultForJoiner { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static AccessCategoryEntitlement Create(
        Guid accessCategoryId,
        Guid accessEntitlementId,
        bool isDefaultForJoiner,
        int sortOrder,
        DateTimeOffset utcNow,
        bool isActive = true)
    {
        if (accessCategoryId == Guid.Empty) throw new ArgumentException("Category is required.", nameof(accessCategoryId));
        if (accessEntitlementId == Guid.Empty) throw new ArgumentException("Entitlement is required.", nameof(accessEntitlementId));
        return new AccessCategoryEntitlement
        {
            Id = Guid.CreateVersion7(),
            AccessCategoryId = accessCategoryId,
            AccessEntitlementId = accessEntitlementId,
            IsDefaultForJoiner = isDefaultForJoiner,
            SortOrder = sortOrder,
            IsActive = isActive,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(bool isDefaultForJoiner, int sortOrder, bool isActive, DateTimeOffset utcNow)
    {
        IsDefaultForJoiner = isDefaultForJoiner;
        SortOrder = sortOrder;
        IsActive = isActive;
        UpdatedAtUtc = utcNow;
    }
}

public sealed class UserAccessEntitlement
{
    private UserAccessEntitlement() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? AccessEntitlementId { get; private set; }
    public string EntitlementKeySnapshot { get; private set; } = null!;
    public string EntitlementNameSnapshot { get; private set; } = null!;
    public UserAccessEntitlementStatus Status { get; private set; }
    public Guid? GrantedFromAccessCaseId { get; private set; }
    public Guid? LastChangedFromAccessCaseId { get; private set; }
    public DateTimeOffset? GrantedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static UserAccessEntitlement CreateActive(
        Guid userId,
        string entitlementKeySnapshot,
        string entitlementNameSnapshot,
        DateTimeOffset utcNow,
        Guid? accessEntitlementId = null,
        Guid? grantedFromAccessCaseId = null)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(entitlementKeySnapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(entitlementNameSnapshot);
        return new UserAccessEntitlement
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            AccessEntitlementId = accessEntitlementId is null || accessEntitlementId == Guid.Empty
                ? null
                : accessEntitlementId,
            EntitlementKeySnapshot = entitlementKeySnapshot.Trim(),
            EntitlementNameSnapshot = entitlementNameSnapshot.Trim(),
            Status = UserAccessEntitlementStatus.Active,
            GrantedFromAccessCaseId = grantedFromAccessCaseId,
            LastChangedFromAccessCaseId = grantedFromAccessCaseId,
            GrantedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void ApplyGrant(Guid accessCaseId, DateTimeOffset utcNow, string? nameSnapshot = null)
    {
        Status = UserAccessEntitlementStatus.Active;
        GrantedFromAccessCaseId ??= accessCaseId;
        LastChangedFromAccessCaseId = accessCaseId;
        GrantedAtUtc ??= utcNow;
        RevokedAtUtc = null;
        if (!string.IsNullOrWhiteSpace(nameSnapshot))
            EntitlementNameSnapshot = nameSnapshot.Trim();
        UpdatedAtUtc = utcNow;
    }

    public void ApplyRemove(Guid accessCaseId, DateTimeOffset utcNow)
    {
        Status = UserAccessEntitlementStatus.Removed;
        LastChangedFromAccessCaseId = accessCaseId;
        RevokedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public void ApplyDisable(Guid accessCaseId, DateTimeOffset utcNow)
    {
        Status = UserAccessEntitlementStatus.Disabled;
        LastChangedFromAccessCaseId = accessCaseId;
        RevokedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }
}
