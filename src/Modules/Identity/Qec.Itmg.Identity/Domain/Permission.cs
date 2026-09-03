using System.Text.RegularExpressions;

namespace Qec.Itmg.Identity.Domain;

public sealed partial class Permission
{
    private Permission()
    {
    }

    public Guid Id { get; private set; }

    public string Key { get; private set; } = null!;

    public string? Description { get; private set; }

    public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();

    public static Permission Create(string key, string? description = null)
    {
        string normalized = NormalizeKey(key);
        EnsureValidKey(normalized);

        return new Permission
        {
            Id = Guid.CreateVersion7(),
            Key = normalized,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
        };
    }

    public static void EnsureValidKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!PermissionKeyPattern().IsMatch(key))
        {
            throw new ArgumentException(
                "Permission key must be resource.action or resource.action.qualifier using lowercase letters, digits, and hyphens.",
                nameof(key));
        }
    }

    private static string NormalizeKey(string key) => key.Trim().ToLowerInvariant();

    [GeneratedRegex(@"^[a-z0-9-]+\.[a-z0-9-]+(\.[a-z0-9-]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex PermissionKeyPattern();
}
