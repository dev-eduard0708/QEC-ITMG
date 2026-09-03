using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;

namespace Qec.Itmg.Identity.Authentication;

public sealed class BreakGlassLoginRequest
{
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public enum BreakGlassLoginFailureReason
{
    Disabled,
    InvalidCredentials,
    UserInactiveOrMissing,
}

public sealed class BreakGlassLoginResult
{
    public bool Succeeded { get; init; }

    public User? User { get; init; }

    public BreakGlassLoginFailureReason? FailureReason { get; init; }

    public static BreakGlassLoginResult Success(User user) => new()
    {
        Succeeded = true,
        User = user,
    };

    public static BreakGlassLoginResult Fail(BreakGlassLoginFailureReason reason) => new()
    {
        Succeeded = false,
        FailureReason = reason,
    };
}

public interface IBreakGlassLoginService
{
    Task<BreakGlassLoginResult> AuthenticateAsync(
        BreakGlassLoginRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class BreakGlassLoginService(
    IOptions<BreakGlassAuthenticationOptions> options,
    IdentityDbContext dbContext) : IBreakGlassLoginService
{
    private readonly PasswordHasher<object> _passwordHasher = new();

    public async Task<BreakGlassLoginResult> AuthenticateAsync(
        BreakGlassLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        BreakGlassAuthenticationOptions settings = options.Value;
        if (!settings.Enabled)
        {
            return BreakGlassLoginResult.Fail(BreakGlassLoginFailureReason.Disabled);
        }

        string username = request.Username?.Trim() ?? string.Empty;
        string password = request.Password ?? string.Empty;
        if (username.Length == 0 || password.Length == 0)
        {
            return BreakGlassLoginResult.Fail(BreakGlassLoginFailureReason.InvalidCredentials);
        }

        BreakGlassAccountOptions? account = settings.Accounts.FirstOrDefault(candidate =>
            string.Equals(candidate.Username?.Trim(), username, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(candidate.PasswordHash)
            && !string.IsNullOrWhiteSpace(candidate.UserUpn));

        if (account is null)
        {
            return BreakGlassLoginResult.Fail(BreakGlassLoginFailureReason.InvalidCredentials);
        }

        PasswordVerificationResult verification = _passwordHasher.VerifyHashedPassword(
            user: new object(),
            hashedPassword: account.PasswordHash,
            providedPassword: password);

        if (verification is PasswordVerificationResult.Failed)
        {
            return BreakGlassLoginResult.Fail(BreakGlassLoginFailureReason.InvalidCredentials);
        }

        string userUpn = account.UserUpn.Trim();
        User? user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Upn == userUpn, cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
        {
            return BreakGlassLoginResult.Fail(BreakGlassLoginFailureReason.UserInactiveOrMissing);
        }

        return BreakGlassLoginResult.Success(user);
    }
}
