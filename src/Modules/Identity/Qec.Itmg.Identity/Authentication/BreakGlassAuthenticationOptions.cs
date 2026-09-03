namespace Qec.Itmg.Identity.Authentication;

public sealed class BreakGlassAuthenticationOptions
{
    public const string SectionName = "Authentication:BreakGlass";

    public bool Enabled { get; set; }

    /// <summary>
    /// Emergency local accounts. Password hashes must come from secrets / local config — never commit real hashes.
    /// </summary>
    public List<BreakGlassAccountOptions> Accounts { get; set; } = [];
}

public sealed class BreakGlassAccountOptions
{
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Maps to an existing Active ITMG <c>User.Upn</c>. Does not grant permissions by itself.
    /// </summary>
    public string UserUpn { get; set; } = string.Empty;

    /// <summary>
    /// ASP.NET Core Identity <see cref="Microsoft.AspNetCore.Identity.PasswordHasher{TUser}"/> hash.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
}
