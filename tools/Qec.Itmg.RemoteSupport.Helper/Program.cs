using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qec.Itmg.RemoteSupport.Helper;

/// <summary>
/// QEC Remote Support Helper — enrollment + MeshCentral agent bootstrap orchestration.
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

        BootstrapConfig? bootstrap = TryLoadBootstrap();
        string? baseUrl = GetArg(args, "--base-url")
            ?? bootstrap?.BaseUrl
            ?? Environment.GetEnvironmentVariable("QEC_ITMG_BASE_URL");
        string? token = GetArg(args, "--token")
            ?? bootstrap?.Token
            ?? Environment.GetEnvironmentVariable("QEC_REMOTE_ENROLLMENT_TOKEN");
        string? tokenFile = GetArg(args, "--token-file");

        if (string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(tokenFile) && File.Exists(tokenFile))
        {
            token = (await File.ReadAllTextAsync(tokenFile)).Trim();
            TryDelete(tokenFile);
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            Console.Error.WriteLine("Missing ITMG base URL. Re-download Support Helper from the Remote Support page.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine("Missing enrollment token. Re-download Support Helper from the Remote Support page.");
            return 2;
        }

        DeviceIdentity device = DetectDevice();
        Console.WriteLine($"Device: {device.DeviceName}");
        Console.WriteLine($"OS: {device.OperatingSystem} {device.OperatingSystemVersion}".Trim());
        Console.WriteLine($"Architecture: {device.Architecture}");
        Console.WriteLine();

        string apiRoot = baseUrl.TrimEnd('/');
        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(120) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Qec.Itmg.RemoteSupport.Helper/{device.HelperVersion}");

        var body = new RedeemRequest(
            token,
            device.DeviceName,
            device.OperatingSystem,
            device.OperatingSystemVersion,
            device.Architecture,
            device.HelperVersion,
            ReportedEngineNodeId: null,
            AgentStatus: "installing");

        token = null;
        TryDeleteBootstrap();

        Console.WriteLine("Registering this computer with IT Support...");
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

        Console.WriteLine($"Registered. Status: {result.ConnectionStatus}");

        if (!string.IsNullOrWhiteSpace(result.AgentDownloadUrl) || !string.IsNullOrWhiteSpace(result.AgentBootstrapUrl))
        {
            string agentUrl = result.AgentBootstrapUrl ?? result.AgentDownloadUrl!;
            Console.WriteLine("Downloading remote support agent...");
            if (!string.IsNullOrWhiteSpace(result.AgentInstallInstructions))
                Console.WriteLine(result.AgentInstallInstructions);

            string agentPath = Path.Combine(Path.GetTempPath(), "QecMeshAgent-" + Guid.NewGuid().ToString("N") + ".exe");
            try
            {
                await using Stream agentStream = await client.GetStreamAsync(agentUrl);
                await using FileStream file = File.Create(agentPath);
                await agentStream.CopyToAsync(file);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Agent download failed: {ex.Message}");
                Console.WriteLine("You can continue chatting with IT. Return to the Remote Support page.");
                return 6;
            }

            Console.WriteLine("Starting agent installer (administrator approval may be required)...");
            try
            {
                using Process? proc = Process.Start(new ProcessStartInfo
                {
                    FileName = agentPath,
                    UseShellExecute = true,
                });
                proc?.WaitForExit(120_000);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not start agent installer: {ex.Message}");
            }

            try { File.Delete(agentPath); } catch { /* ignore */ }

            Console.WriteLine("Waiting for remote agent registration...");
            await Task.Delay(TimeSpan.FromSeconds(8));

            if (!string.IsNullOrWhiteSpace(result.ReportSecret))
            {
                try
                {
                    await client.PostAsJsonAsync(
                        $"{apiRoot}/api/v1/remote-support/endpoints/{result.EndpointId}/status",
                        new
                        {
                            reportSecret = result.ReportSecret,
                            connectionStatus = "AgentInstalling",
                            agentVersion = device.HelperVersion,
                        },
                        JsonOpts);
                }
                catch
                {
                    // best-effort
                }
            }
        }
        else if (result.WaitingForRemoteAgent)
        {
            Console.WriteLine("Remote agent package is not configured yet.");
            Console.WriteLine("Your computer was detected. Continue chatting with IT.");
        }
        else
        {
            Console.WriteLine("Device is ready for remote support. Return to the Remote Support page.");
        }

        Console.WriteLine();
        Console.WriteLine("You can close this window.");
        return 0;
    }

    private static BootstrapConfig? TryLoadBootstrap()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "enrollment.bootstrap.json");
        if (!File.Exists(path))
            return null;
        try
        {
            return JsonSerializer.Deserialize<BootstrapConfig>(File.ReadAllText(path), JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private static void TryDeleteBootstrap()
    {
        TryDelete(Path.Combine(AppContext.BaseDirectory, "enrollment.bootstrap.json"));
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
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
        const string helperVersion = "1.1.0";
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

    private sealed record BootstrapConfig(string? BaseUrl, string? Token, DateTimeOffset? ExpiresAtUtc, Guid? EnrollmentId);

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
        string? ReportedEngineNodeId,
        string? AgentStatus);

    private sealed record RedeemResponse(
        Guid EndpointId,
        Guid RemoteSessionRequestId,
        string DeviceName,
        string ConnectionStatus,
        bool WaitingForRemoteAgent,
        string? AgentDownloadUrl,
        string? AgentInstallInstructions,
        string? AgentBootstrapUrl,
        string? ReportSecret);
}
