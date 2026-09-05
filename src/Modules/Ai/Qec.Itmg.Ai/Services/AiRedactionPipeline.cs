using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Qec.Itmg.Contracts.Ai;

namespace Qec.Itmg.Ai.Services;

public sealed partial class AiRedactionPipeline(IOptions<AiOptions> options) : IAiRedactionPipeline
{
    public bool MayIncludeInModelContext(AiDataClassification classification, bool confidentialAllowed)
    {
        AiOptions opts = options.Value;
        return classification switch
        {
            AiDataClassification.Public or AiDataClassification.Internal => true,
            AiDataClassification.Confidential => confidentialAllowed && opts.AllowConfidentialInContext,
            AiDataClassification.Restricted => opts.AllowRestrictedInContext, // default false
            _ => false,
        };
    }

    public AiRedactionResult Redact(string? input, AiDataClassification classification)
    {
        if (string.IsNullOrEmpty(input))
            return new(string.Empty, 0, []);

        if (!MayIncludeInModelContext(classification, options.Value.AllowConfidentialInContext))
            return new("[REDACTED: classification denied]", 1, ["classification"]);

        string text = input;
        List<string> categories = [];
        int count = 0;

        (text, count, categories) = Apply(text, count, categories, "password", PasswordPattern());
        (text, count, categories) = Apply(text, count, categories, "token", TokenPattern());
        (text, count, categories) = Apply(text, count, categories, "secret_ref", SecretRefPattern());
        (text, count, categories) = Apply(text, count, categories, "connection_string", ConnectionStringPattern());
        (text, count, categories) = Apply(text, count, categories, "authorization", AuthorizationPattern());
        (text, count, categories) = Apply(text, count, categories, "api_key", ApiKeyPattern());
        (text, count, categories) = Apply(text, count, categories, "email", EmailPattern(), replaceWith: "[REDACTED:email]");
        (text, count, categories) = Apply(text, count, categories, "phone", PhonePattern(), replaceWith: "[REDACTED:phone]");

        return new(text, count, categories.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static (string text, int count, List<string> categories) Apply(
        string text,
        int count,
        List<string> categories,
        string category,
        Regex regex,
        string replaceWith = "[REDACTED]")
    {
        MatchCollection matches = regex.Matches(text);
        if (matches.Count == 0) return (text, count, categories);
        categories.Add(category);
        return (regex.Replace(text, replaceWith), count + matches.Count, categories);
    }

    [GeneratedRegex(@"(?i)(password|passwd|pwd)\s*[:=]\s*\S+")]
    private static partial Regex PasswordPattern();

    [GeneratedRegex(@"(?i)(bearer\s+[a-z0-9\-_\.=+/]{8,}|access_token\s*[:=]\s*\S+|refresh_token\s*[:=]\s*\S+)")]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"(?i)(credentialreference|secretreference|client_secret)\s*[:=]\s*\S+")]
    private static partial Regex SecretRefPattern();

    [GeneratedRegex(@"(?i)(server|data source)\s*=[^;]+;.*(password|pwd)\s*=[^;]+")]
    private static partial Regex ConnectionStringPattern();

    [GeneratedRegex(@"(?i)authorization\s*[:=]\s*\S+")]
    private static partial Regex AuthorizationPattern();

    [GeneratedRegex(@"(?i)(api[_-]?key|x-api-key)\s*[:=]\s*\S+")]
    private static partial Regex ApiKeyPattern();

    [GeneratedRegex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\+?\d[\d\-\s()]{7,}\d")]
    private static partial Regex PhonePattern();
}
