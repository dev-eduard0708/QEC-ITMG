namespace Qec.Itmg.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public bool Enabled { get; set; }

    /// <summary>Disabled | OpenAICompatible</summary>
    public string ProviderKind { get; set; } = "Disabled";

    public string BaseUrl { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    /// <summary>Secret-store reference name only — never an API key.</summary>
    public string CredentialReference { get; set; } = string.Empty;

    /// <summary>When true, Confidential fields may be included after redaction if user is authorized.</summary>
    public bool AllowConfidentialInContext { get; set; }

    /// <summary>Restricted content is always denied from model context unless explicitly true (default false).</summary>
    public bool AllowRestrictedInContext { get; set; }

    /// <summary>Default OFF — never log raw prompts/responses.</summary>
    public bool PersistPromptLogs { get; set; }

    public bool IsConfigured =>
        Enabled
        && !string.Equals(ProviderKind, "Disabled", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(ModelName)
        && !string.IsNullOrWhiteSpace(CredentialReference);
}
