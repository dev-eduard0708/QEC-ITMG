using System.Security.Cryptography;
using System.Text;

namespace Qec.Itmg.RemoteSupport.Domain;

public sealed class RemoteEndpointEnrollment
{
    private RemoteEndpointEnrollment()
    {
    }

    public Guid Id { get; private set; }
    public Guid RemoteSessionRequestId { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RedeemedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Guid? EndpointId { get; private set; }
    public string? CreatedFromIp { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public bool IsRedeemable(DateTimeOffset utcNow) =>
        RevokedAtUtc is null
        && RedeemedAtUtc is null
        && utcNow <= ExpiresAtUtc;

    /// <summary>
    /// Creates enrollment and returns the plaintext token once. Only the hash is persisted.
    /// </summary>
    public static (RemoteEndpointEnrollment Enrollment, string PlainToken) Issue(
        Guid remoteSessionRequestId,
        Guid userId,
        DateTimeOffset utcNow,
        TimeSpan lifetime,
        string? createdFromIp)
    {
        if (remoteSessionRequestId == Guid.Empty)
            throw new ArgumentException("Session is required.", nameof(remoteSessionRequestId));
        if (userId == Guid.Empty)
            throw new ArgumentException("User is required.", nameof(userId));
        if (lifetime < TimeSpan.FromMinutes(1))
            lifetime = TimeSpan.FromMinutes(1);

        byte[] bytes = RandomNumberGenerator.GetBytes(32); // 256-bit
        string plain = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var entity = new RemoteEndpointEnrollment
        {
            Id = Guid.CreateVersion7(),
            RemoteSessionRequestId = remoteSessionRequestId,
            UserId = userId,
            TokenHash = HashToken(plain),
            CreatedAtUtc = utcNow,
            ExpiresAtUtc = utcNow.Add(lifetime),
            CreatedFromIp = string.IsNullOrWhiteSpace(createdFromIp) ? null : createdFromIp.Trim()[..Math.Min(64, createdFromIp.Trim().Length)],
        };
        return (entity, plain);
    }

    public void Redeem(Guid endpointId, DateTimeOffset utcNow)
    {
        if (!IsRedeemable(utcNow))
            throw new InvalidOperationException("Enrollment token is not redeemable.");
        if (endpointId == Guid.Empty)
            throw new ArgumentException("Endpoint is required.", nameof(endpointId));

        RedeemedAtUtc = utcNow;
        EndpointId = endpointId;
    }

    public void Revoke(DateTimeOffset utcNow)
    {
        if (RevokedAtUtc is not null) return;
        RevokedAtUtc = utcNow;
    }

    public static string HashToken(string plainToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainToken);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken.Trim()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
