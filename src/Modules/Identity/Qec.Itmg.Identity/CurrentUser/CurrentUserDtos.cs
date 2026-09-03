namespace Qec.Itmg.Identity.CurrentUser;

public sealed record CurrentUserRoleDto(Guid Id, string Name);

public sealed record CurrentUserDto(
    Guid Id,
    string Upn,
    string DisplayName,
    string UserType,
    string? TimeZone,
    string AuthMethod,
    IReadOnlyList<CurrentUserRoleDto> Roles,
    IReadOnlyList<string> Permissions);
