using System.Security.Cryptography;
using System.Text;

namespace Qec.Itmg.RemoteSupport.Domain;

public enum RemoteEndpointPairingStatus
{
    Pending = 0,
    Authorized = 1,
    Rejected = 2,
    Expired = 3,
    Completed = 4,
}

/// <summary>
/// Device-authorization style pairing (pre-session). Device secret is stored hashed only.
/// </summary>
public sealed class RemoteEndpointPairing
{
    private static readonly char[] UserCodeAlphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    private RemoteEndpointPairing()
    {
    }

    public Guid Id { get; private set; }
    public string DeviceSecretHash { get; private set; } = null!;
    public string UserCode { get; private set; } = null!;
    public RemoteEndpointPairingStatus Status { get; private set; }
    public Guid? AuthorizedUserId { get; private set; }
    public Guid? EndpointId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? AuthorizedAtUtc { get; private set; }
    public DateTimeOffset? RejectedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? CreatedFromIp { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public bool IsPending(DateTimeOffset utcNow) =>
        Status == RemoteEndpointPairingStatus.Pending && utcNow <= ExpiresAtUtc;

    public bool IsAuthorizedAwaitingDevice(DateTimeOffset utcNow) =>
        Status == RemoteEndpointPairingStatus.Authorized
        && CompletedAtUtc is null
        && utcNow <= ExpiresAtUtc;

    public static (RemoteEndpointPairing Pairing, string DeviceSecret, string UserCode) Start(
        DateTimeOffset utcNow,
        TimeSpan lifetime,
        string? createdFromIp)
    {
        if (lifetime < TimeSpan.FromMinutes(1))
            lifetime = TimeSpan.FromMinutes(1);

        byte[] secretBytes = RandomNumberGenerator.GetBytes(32); // 256-bit
        string deviceSecret = Convert.ToBase64String(secretBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        string userCode = GenerateUserCode();

        var entity = new RemoteEndpointPairing
        {
            Id = Guid.CreateVersion7(),
            DeviceSecretHash = HashSecret(deviceSecret),
            UserCode = userCode,
            Status = RemoteEndpointPairingStatus.Pending,
            CreatedAtUtc = utcNow,
            ExpiresAtUtc = utcNow.Add(lifetime),
            CreatedFromIp = TruncateIp(createdFromIp),
        };
        return (entity, deviceSecret, userCode);
    }

    public void Authorize(Guid userId, DateTimeOffset utcNow)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User is required.", nameof(userId));
        if (Status == RemoteEndpointPairingStatus.Rejected)
            throw new InvalidOperationException("Pairing was cancelled.");
        if (Status == RemoteEndpointPairingStatus.Completed)
            throw new InvalidOperationException("Pairing already completed.");
        if (utcNow > ExpiresAtUtc || Status == RemoteEndpointPairingStatus.Expired)
        {
            Status = RemoteEndpointPairingStatus.Expired;
            throw new InvalidOperationException("Pairing code has expired.");
        }

        if (Status == RemoteEndpointPairingStatus.Authorized
            && AuthorizedUserId == userId)
            return;

        if (Status != RemoteEndpointPairingStatus.Pending)
            throw new InvalidOperationException("Pairing is not awaiting approval.");

        Status = RemoteEndpointPairingStatus.Authorized;
        AuthorizedUserId = userId;
        AuthorizedAtUtc = utcNow;
    }

    public void Reject(DateTimeOffset utcNow)
    {
        if (Status is RemoteEndpointPairingStatus.Completed or RemoteEndpointPairingStatus.Rejected)
            return;
        Status = RemoteEndpointPairingStatus.Rejected;
        RejectedAtUtc = utcNow;
    }

    public void MarkExpired(DateTimeOffset utcNow)
    {
        if (Status is RemoteEndpointPairingStatus.Completed or RemoteEndpointPairingStatus.Rejected)
            return;
        Status = RemoteEndpointPairingStatus.Expired;
        _ = utcNow;
    }

    public void Complete(Guid endpointId, DateTimeOffset utcNow)
    {
        if (endpointId == Guid.Empty)
            throw new ArgumentException("Endpoint is required.", nameof(endpointId));
        if (Status != RemoteEndpointPairingStatus.Authorized || AuthorizedUserId is null)
            throw new InvalidOperationException("Pairing is not authorized.");
        if (utcNow > ExpiresAtUtc)
        {
            Status = RemoteEndpointPairingStatus.Expired;
            throw new InvalidOperationException("Pairing code has expired.");
        }

        Status = RemoteEndpointPairingStatus.Completed;
        EndpointId = endpointId;
        CompletedAtUtc = utcNow;
    }

    public bool MatchesDeviceSecret(string? deviceSecret) =>
        !string.IsNullOrWhiteSpace(deviceSecret)
        && CryptographicEquals(DeviceSecretHash, HashSecret(deviceSecret));

    public static string HashSecret(string plain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plain);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(plain.Trim()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string NormalizeUserCode(string code) =>
        new string(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string GenerateUserCode()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        char[] chars = new char[9];
        chars[4] = '-';
        for (int i = 0; i < 4; i++)
            chars[i] = UserCodeAlphabet[bytes[i] % UserCodeAlphabet.Length];
        for (int i = 0; i < 4; i++)
            chars[5 + i] = UserCodeAlphabet[bytes[4 + i] % UserCodeAlphabet.Length];
        return new string(chars);
    }

    private static string? TruncateIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        string t = ip.Trim();
        return t.Length <= 64 ? t : t[..64];
    }

    private static bool CryptographicEquals(string a, string b)
    {
        byte[] left = Encoding.UTF8.GetBytes(a);
        byte[] right = Encoding.UTF8.GetBytes(b);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
