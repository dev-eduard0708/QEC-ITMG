using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qec.Itmg.RemoteSupport.Helper;

/// <summary>
/// QEC Remote Support Helper — bootstrap only.
/// Redeems a one-time ITMG enrollment token, reports minimal device identity,
/// and surfaces configured MeshCentral agent download instructions.
/// Does NOT implement remote desktop / screen transport (MeshCentral owns that).
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("QEC Remote Support Helper");
        Console.WriteLine("This helper connects this computer to QEC IT Support for one support request.");
        Console.WriteLine("You will still be asked before remote control begins.");
        Console.WriteLine();

        string? baseUrl = GetArg(args, "--base-url") ?? Environment.GetEnvironmentVariable("QEC_ITMG_BASE_URL");
        string? token = GetArg(args, "--token") ?? Environment.GetEnvironmentVariable("QEC_REMOTE_ENROLLMENT_TOKEN");
        string? tokenFile = GetArg(args, "--token-file");

        if (string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(tokenFile) && File.Exists(tokenFile))
        {
            token = (await File.ReadAllTextAsync(tokenFile)).Trim();
            try { File.Delete(tokenFile); } catch { /* best-effort wipe */ }
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            Console.Error.WriteLine("Missing --base-url or QEC_ITMG_BASE_URL (e.g. https://itmg.example).");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine("Missing enrollment token (--token / --token-file / QEC_REMOTE_ENROLLMENT_TOKEN).");
            Console.Error.WriteLine("Do not share enrollment tokens. Obtain a fresh one from the Remote Support page.");
            return 2;
        }

        DeviceIdentity device = DetectDevice();
        Console.WriteLine($"Device: {device.DeviceName}");
        Console.WriteLine($"OS: {device.OperatingSystem} {device.OperatingSystemVersion}".Trim());
        Console.WriteLine($"Architecture: {device.Architecture}");
        Console.WriteLine();

        string apiRoot = baseUrl.TrimEnd('/');
        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Qec.Itmg.RemoteSupport.Helper/{device.HelperVersion}");

        var body = new RedeemRequest(
            token,
            device.DeviceName,
            device.OperatingSystem,
            device.OperatingSystemVersion,
            device.Architecture,
            device.HelperVersion,
            ReportedEngineNodeId: null);

        // Clear local token reference ASAP — never log it.
        token = null;

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(
                $"{apiRoot}/api/v1/remote-support/enrollments/redeem",
                body,
                JsonOpts);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not reach ITMG: {ex.Message}");
            return 3;
        }

        string payload = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"Enrollment failed ({(int)response.StatusCode}).");
            Console.Error.WriteLine(SafeErrorMessage(payload));
            return 4;
        }

        RedeemResponse? result = JsonSerializer.Deserialize<RedeemResponse>(payload, JsonOpts);
        if (result is null)
        {
            Console.Error.WriteLine("Unexpected response from ITMG.");
            return 5;
        }

        Console.WriteLine($"Registered with IT Support. Status: {result.ConnectionStatus}");
        if (result.WaitingForRemoteAgent)
        {
            Console.WriteLine("Waiting for remote agent.");
            if (!string.IsNullOrWhiteSpace(result.AgentDownloadUrl))
            {
                Console.WriteLine("Install the configured remote support agent:");
                Console.WriteLine(result.AgentDownloadUrl);
            }
            else
            {
                Console.WriteLine("No agent download is configured yet. Return to the Remote Support page and continue chatting with IT.");
            }

            if (!string.IsNullOrWhiteSpace(result.AgentInstallInstructions))
                Console.WriteLine(result.AgentInstallInstructions);
        }
        else
        {
            Console.WriteLine("Device is ready for remote support. Return to the Remote Support page.");
        }

        Console.WriteLine();
        Console.WriteLine("You can close this window.");
        return 0;
    }

    private static DeviceIdentity DetectDevice()
    {
        string name = Environment.MachineName;
        if (string.IsNullOrWhiteSpace(name))
            name = "Unknown-PC";

        string os = OperatingSystem.IsWindows() ? "Windows"
            : OperatingSystem.IsMacOS() ? "macOS"
            : OperatingSystem.IsLinux() ? "Linux"
            : "Unknown";

        string? version = RuntimeInformation.OSDescription;
        string arch = RuntimeInformation.OSArchitecture.ToString();
        const string helperVersion = "1.0.0";
        return new DeviceIdentity(name, os, version, arch, helperVersion);
    }

    private static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static string SafeErrorMessage(string payload)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("message", out JsonElement msg)
                && msg.ValueKind == JsonValueKind.String)
                return msg.GetString() ?? "Request failed.";
        }
        catch
        {
            // ignore
        }

        return "Request failed.";
    }

    private sealed record DeviceIdentity(
        string DeviceName,
        string OperatingSystem,
        string? OperatingSystemVersion,
        string Architecture,
        string HelperVersion);

    private sealed record RedeemRequest(
        string Token,
        string DeviceName,
        string OperatingSystem,
        string? OperatingSystemVersion,
        string? Architecture,
        string? HelperVersion,
        string? ReportedEngineNodeId);

    private sealed record RedeemResponse(
        Guid EndpointId,
        Guid RemoteSessionRequestId,
        string DeviceName,
        string ConnectionStatus,
        bool WaitingForRemoteAgent,
        string? AgentDownloadUrl,
        string? AgentInstallInstructions);
}
