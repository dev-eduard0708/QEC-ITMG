using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Identity.Domain;

namespace Qec.Itmg.Identity.Admin;

internal static class AdminAuditComposer
{
    public readonly record struct UserAuditState(
        Guid Id,
        string Upn,
        string DisplayName,
        UserType UserType,
        UserStatus Status,
        string? TimeZone,
        string? DirectoryObjectId);

    public readonly record struct RoleAuditState(
        Guid Id,
        string Name,
        string? Description);

    public static BusinessAuditEntry UserCreated(User user) =>
        new()
        {
            AggregateType = AuditAggregateType.User,
            AggregateId = user.Id,
            BusinessNumber = user.Upn,
            Action = BusinessAuditAction.Created,
            NewValue = user.DisplayName,
            Source = AuditSource.Api,
        };

    public static UserAuditState CaptureUser(User user) =>
        new(user.Id, user.Upn, user.DisplayName, user.UserType, user.Status, user.TimeZone, user.DirectoryObjectId);

    public static IEnumerable<BusinessAuditEntry> UserProfileChanges(UserAuditState before, User after)
    {
        if (!string.Equals(before.DisplayName, after.DisplayName, StringComparison.Ordinal))
        {
            yield return Field(
                AuditAggregateType.User,
                after.Id,
                after.Upn,
                BusinessAuditAction.Updated,
                "DisplayName",
                before.DisplayName,
                after.DisplayName);
        }

        if (before.UserType != after.UserType)
        {
            yield return Field(
                AuditAggregateType.User,
                after.Id,
                after.Upn,
                BusinessAuditAction.Updated,
                "UserType",
                before.UserType.ToString(),
                after.UserType.ToString());
        }

        if (!string.Equals(before.TimeZone, after.TimeZone, StringComparison.Ordinal))
        {
            yield return Field(
                AuditAggregateType.User,
                after.Id,
                after.Upn,
                BusinessAuditAction.Updated,
                "TimeZone",
                before.TimeZone,
                after.TimeZone);
        }

        if (!string.Equals(before.DirectoryObjectId, after.DirectoryObjectId, StringComparison.Ordinal))
        {
            yield return Field(
                AuditAggregateType.User,
                after.Id,
                after.Upn,
                BusinessAuditAction.Updated,
                "DirectoryObjectId",
                before.DirectoryObjectId,
                after.DirectoryObjectId);
        }

        if (before.Status != after.Status)
        {
            yield return Field(
                AuditAggregateType.User,
                after.Id,
                after.Upn,
                BusinessAuditAction.StatusChanged,
                "Status",
                before.Status.ToString(),
                after.Status.ToString());
        }
    }

    public static BusinessAuditEntry RoleCreated(Role role) =>
        new()
        {
            AggregateType = AuditAggregateType.Role,
            AggregateId = role.Id,
            BusinessNumber = role.Name,
            Action = BusinessAuditAction.Created,
            NewValue = role.Name,
            Source = AuditSource.Api,
        };

    public static RoleAuditState CaptureRole(Role role) =>
        new(role.Id, role.Name, role.Description);

    public static IEnumerable<BusinessAuditEntry> RoleChanges(RoleAuditState before, Role after)
    {
        if (!string.Equals(before.Name, after.Name, StringComparison.Ordinal))
        {
            yield return Field(
                AuditAggregateType.Role,
                after.Id,
                after.Name,
                BusinessAuditAction.Updated,
                "Name",
                before.Name,
                after.Name);
        }

        if (!string.Equals(before.Description, after.Description, StringComparison.Ordinal))
        {
            yield return Field(
                AuditAggregateType.Role,
                after.Id,
                after.Name,
                BusinessAuditAction.Updated,
                "Description",
                before.Description,
                after.Description);
        }
    }

    public static BusinessAuditEntry RoleAssigned(Guid userId, string? upn, Guid roleId, string roleName) =>
        new()
        {
            AggregateType = AuditAggregateType.User,
            AggregateId = userId,
            BusinessNumber = upn,
            Action = BusinessAuditAction.Assigned,
            FieldName = "Role",
            NewValue = $"{roleName}|{roleId:D}",
            Source = AuditSource.Api,
        };

    public static BusinessAuditEntry RoleUnassigned(Guid userId, string? upn, Guid roleId, string roleName) =>
        new()
        {
            AggregateType = AuditAggregateType.User,
            AggregateId = userId,
            BusinessNumber = upn,
            Action = BusinessAuditAction.Unassigned,
            FieldName = "Role",
            OldValue = $"{roleName}|{roleId:D}",
            Source = AuditSource.Api,
        };

    public static BusinessAuditEntry PermissionGranted(Guid roleId, string roleName, Guid permissionId, string permissionKey) =>
        new()
        {
            AggregateType = AuditAggregateType.Role,
            AggregateId = roleId,
            BusinessNumber = roleName,
            Action = BusinessAuditAction.Linked,
            FieldName = "Permission",
            NewValue = $"{permissionKey}|{permissionId:D}",
            Source = AuditSource.Api,
        };

    public static BusinessAuditEntry PermissionRevoked(Guid roleId, string roleName, Guid permissionId, string permissionKey) =>
        new()
        {
            AggregateType = AuditAggregateType.Role,
            AggregateId = roleId,
            BusinessNumber = roleName,
            Action = BusinessAuditAction.Unlinked,
            FieldName = "Permission",
            OldValue = $"{permissionKey}|{permissionId:D}",
            Source = AuditSource.Api,
        };

    private static BusinessAuditEntry Field(
        AuditAggregateType aggregateType,
        Guid aggregateId,
        string? businessNumber,
        BusinessAuditAction action,
        string fieldName,
        string? oldValue,
        string? newValue) =>
        new()
        {
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            BusinessNumber = businessNumber,
            Action = action,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            Source = AuditSource.Api,
        };
}
