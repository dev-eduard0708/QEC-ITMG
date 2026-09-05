namespace Qec.Itmg.Contracts.Secrets;

/// <summary>
/// Resolves secret-store references to secret values.
/// Configuration and persistence must store CredentialReference / SecretReference names only —
/// never passwords, API keys, client secrets, or tokens.
/// </summary>
public interface ISecretResolver
{
    /// <summary>
    /// Resolves a credential/secret reference. Returns null when the reference is empty or unresolved.
    /// Implementations must never log the resolved value.
    /// </summary>
    Task<string?> ResolveAsync(string? credentialReference, CancellationToken cancellationToken = default);

    /// <summary>True when a non-empty reference can be resolved without exposing the value.</summary>
    Task<bool> CanResolveAsync(string? credentialReference, CancellationToken cancellationToken = default);
}
