using Qec.Itmg.Identity.Domain;

namespace Qec.Itmg.Identity.Admin;

public sealed record AdminRoleSummaryDto(Guid Id, string Name);

public sealed record AdminUserDto(
    Guid Id,
    string Upn,
    string DisplayName,
    string Status,
    string UserType,
    string? DirectoryObjectId,
    string? TimeZone,
    string RowVersion,
    IReadOnlyList<AdminRoleSummaryDto> Roles);

public sealed record CreateAdminUserRequest(
    string Upn,
    string DisplayName,
    string UserType,
    string? TimeZone,
    string? DirectoryObjectId);

public sealed record UpdateAdminUserRequest(
    string DisplayName,
    string UserType,
    string Status,
    string? TimeZone,
    string? DirectoryObjectId,
    string RowVersion);

public sealed record ReplaceUserRolesRequest(IReadOnlyList<Guid> RoleIds);

public sealed record AdminPermissionDto(Guid Id, string Key, string? Description);

public sealed record AdminRoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    string RowVersion,
    int PermissionCount,
    IReadOnlyList<AdminPermissionDto> Permissions);

public sealed record CreateAdminRoleRequest(string Name, string? Description);

public sealed record UpdateAdminRoleRequest(string Name, string? Description, string RowVersion);

public sealed record ReplaceRolePermissionsRequest(IReadOnlyList<Guid> PermissionIds);

internal static class AdminDtoMapper
{
    public static string ToBase64(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static bool TryParseRowVersion(string? value, out byte[] rowVersion)
    {
        rowVersion = Array.Empty<byte>();
        if (value is null)
        {
            return false;
        }

        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            // InMemory and unsaved entities may still have an empty concurrency token.
            return true;
        }

        try
        {
            rowVersion = Convert.FromBase64String(trimmed);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool MatchesRowVersion(byte[] current, string expectedBase64)
    {
        if (!TryParseRowVersion(expectedBase64, out byte[] expected))
        {
            return false;
        }

        return current.AsSpan().SequenceEqual(expected);
    }

    public static bool TryParseUserType(string? value, out UserType userType) =>
        Enum.TryParse(value, ignoreCase: true, out userType) && Enum.IsDefined(userType);

    public static bool TryParseUserStatus(string? value, out UserStatus status) =>
        Enum.TryParse(value, ignoreCase: true, out status) && Enum.IsDefined(status);
}
