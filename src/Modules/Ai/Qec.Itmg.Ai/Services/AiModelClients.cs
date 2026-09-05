using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qec.Itmg.Contracts.Ai;
using Qec.Itmg.Contracts.Secrets;

namespace Qec.Itmg.Ai.Services;

public sealed class AiHealthState
{
    private readonly ConcurrentDictionary<string, (DateTimeOffset? Ok, DateTimeOffset? Fail, string? Err)> _state = new();

    public void RecordSuccess(string key, DateTimeOffset at) =>
        _state.AddOrUpdate(key, _ => (at, null, null), (_, prev) => (at, prev.Fail, null));

    public void RecordFailure(string key, DateTimeOffset at, string error) =>
        _state.AddOrUpdate(key, _ => (null, at, Trunc(error)), (_, prev) => (prev.Ok, at, Trunc(error)));

    public (DateTimeOffset? Ok, DateTimeOffset? Fail, string? Err) Get(string key) =>
        _state.TryGetValue(key, out var v) ? v : default;

    private static string Trunc(string e) => e.Length <= 400 ? e : e[..400];
}

public sealed class DisabledAiModelClient(IOptions<AiOptions> options, AiHealthState health) : IAiModelClient
{
    public AiReadiness GetReadiness()
    {
        AiOptions opts = options.Value;
        var h = health.Get("ai");
        string status = !opts.Enabled ? "Disabled" : opts.IsConfigured ? "Configured" : "NotConfigured";
        if (!opts.Enabled) status = "Disabled";
        else if (!opts.IsConfigured) status = "NotConfigured";
        return new(opts.Enabled, opts.IsConfigured, opts.ProviderKind, opts.ModelName, status, h.Ok, h.Fail, h.Err);
    }

    public Task<AiModelResponse> CompleteAsync(AiModelRequest request, CancellationToken cancellationToken = default) =>
        Task.FromException<AiModelResponse>(
            new InvalidOperationException("AI assistance is disabled. Explicit QEC configuration is required."));
}

/// <summary>OpenAI-compatible chat completions client. Credentials resolved via ISecretResolver only.</summary>
public sealed class OpenAiCompatibleAiModelClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AiOptions> options,
    ISecretResolver secrets,
    AiHealthState health,
    ILogger<OpenAiCompatibleAiModelClient> logger) : IAiModelClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public AiReadiness GetReadiness()
    {
        AiOptions opts = options.Value;
        var h = health.Get("ai");
        string status;
        if (!opts.Enabled) status = "Disabled";
        else if (!opts.IsConfigured) status = "NotConfigured";
        else if (h.Fail is not null && (h.Ok is null || h.Fail > h.Ok)) status = "Unhealthy";
        else if (h.Ok is not null) status = "Healthy";
        else status = "Configured";
        return new(opts.Enabled, opts.IsConfigured, opts.ProviderKind, opts.ModelName, status, h.Ok, h.Fail, h.Err);
    }

    public async Task<AiModelResponse> CompleteAsync(AiModelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        AiOptions opts = options.Value;
        if (!opts.Enabled || !opts.IsConfigured)
            throw new InvalidOperationException("AI provider is not enabled/configured.");

        string? apiKey = await secrets.ResolveAsync(opts.CredentialReference, cancellationToken);
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("AI CredentialReference could not be resolved.");

        HttpClient client = httpClientFactory.CreateClient("ai-openai");
        client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        client.Timeout = TimeSpan.FromSeconds(60);

        var payload = new
        {
            model = opts.ModelName,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            messages = request.Messages.Select(m => new Dictionary<string, object?>
            {
                ["role"] = m.Role,
                ["content"] = m.Content,
                ["tool_call_id"] = m.ToolCallId,
                ["name"] = m.Name,
                ["tool_calls"] = m.ToolCalls?.Select(t => new
                {
                    id = t.Id,
                    type = "function",
                    function = new { name = t.Name, arguments = t.ArgumentsJson },
                }).ToArray(),
            }).ToList(),
            tools = request.Tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = JsonSerializer.Deserialize<JsonElement>(t.ParametersJsonSchema),
                },
            }).ToArray(),
        };

        using HttpRequestMessage httpReq = new(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json"),
        };

        try
        {
            using HttpResponseMessage response = await client.SendAsync(httpReq, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                health.RecordFailure("ai", DateTimeOffset.UtcNow, $"HTTP {(int)response.StatusCode}");
                logger.LogWarning("AI provider returned {Status}", (int)response.StatusCode);
                throw new InvalidOperationException($"AI provider request failed ({(int)response.StatusCode}).");
            }

            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement choice = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
            string? content = choice.TryGetProperty("content", out JsonElement c) && c.ValueKind == JsonValueKind.String
                ? c.GetString()
                : null;
            List<AiToolCall> toolCalls = [];
            if (choice.TryGetProperty("tool_calls", out JsonElement tcs) && tcs.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement tc in tcs.EnumerateArray())
                {
                    string id = tc.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");
                    JsonElement fn = tc.GetProperty("function");
                    toolCalls.Add(new AiToolCall(
                        id,
                        fn.GetProperty("name").GetString() ?? "unknown",
                        fn.GetProperty("arguments").GetString() ?? "{}"));
                }
            }

            string? finish = doc.RootElement.GetProperty("choices")[0].TryGetProperty("finish_reason", out JsonElement fr)
                ? fr.GetString()
                : null;
            string? reqId = response.Headers.TryGetValues("x-request-id", out var vals) ? vals.FirstOrDefault() : null;
            health.RecordSuccess("ai", DateTimeOffset.UtcNow);
            return new AiModelResponse(content, toolCalls, finish, reqId);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            health.RecordFailure("ai", DateTimeOffset.UtcNow, "provider-error");
            logger.LogError(ex, "AI provider call failed");
            throw;
        }
    }
}

public sealed class ConfigurableAiModelClient(
    IOptions<AiOptions> options,
    DisabledAiModelClient disabled,
    OpenAiCompatibleAiModelClient openAi) : IAiModelClient
{
    public AiReadiness GetReadiness()
    {
        AiOptions opts = options.Value;
        if (opts.Enabled
            && opts.ProviderKind.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase)
            && opts.IsConfigured)
            return openAi.GetReadiness();
        return disabled.GetReadiness();
    }

    public Task<AiModelResponse> CompleteAsync(AiModelRequest request, CancellationToken cancellationToken = default)
    {
        AiOptions opts = options.Value;
        if (opts.Enabled
            && opts.ProviderKind.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase)
            && opts.IsConfigured)
            return openAi.CompleteAsync(request, cancellationToken);
        return disabled.CompleteAsync(request, cancellationToken);
    }
}
