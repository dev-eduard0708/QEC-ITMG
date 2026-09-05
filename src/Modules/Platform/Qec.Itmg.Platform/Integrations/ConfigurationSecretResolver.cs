using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qec.Itmg.Contracts.Secrets;

namespace Qec.Itmg.Platform.Integrations;

/// <summary>
/// Resolves CredentialReference from environment (ITMG_SECRET_{REF}) or configuration Secrets:{REF}.
/// Never logs resolved values. Does not implement a custom vault.
/// </summary>
public sealed class ConfigurationSecretResolver(
    IConfiguration configuration,
    ILogger<ConfigurationSecretResolver> logger) : ISecretResolver
{
    public Task<string?> ResolveAsync(string? credentialReference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(credentialReference))
            return Task.FromResult<string?>(null);

        string reference = credentialReference.Trim();
        string envKey = $"ITMG_SECRET_{NormalizeEnv(reference)}";
        string? fromEnv = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrEmpty(fromEnv))
            return Task.FromResult<string?>(fromEnv);

        string? fromConfig = configuration[$"Secrets:{reference}"]
            ?? configuration.GetSection("Secrets")[reference];
        if (!string.IsNullOrEmpty(fromConfig))
            return Task.FromResult<string?>(fromConfig);

        logger.LogDebug("Secret reference {Reference} was not resolved.", reference);
        return Task.FromResult<string?>(null);
    }

    public async Task<bool> CanResolveAsync(string? credentialReference, CancellationToken cancellationToken = default)
    {
        string? value = await ResolveAsync(credentialReference, cancellationToken);
        return !string.IsNullOrEmpty(value);
    }

    private static string NormalizeEnv(string reference) =>
        reference.Replace(':', '_').Replace('-', '_').Replace('/', '_').ToUpperInvariant();
}
