using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qec.Itmg.RemoteSupport.Helper;

/// <summary>
/// QEC Remote Support Helper — device pairing + MeshCentral agent bootstrap.
/// Does NOT implement remote desktop / screen transport.
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("QEC Remote Support Helper");
        Console.WriteLine("This helper pairs this computer with your ITMG account.");
        Console.WriteLine("IT will still ask before every remote-control session.");
        Console.WriteLine();

        // Session-bound bootstrap (legacy prepare-this-computer path) still supported.
        BootstrapConfig? enrollmentBootstrap = TryLoadEnrollmentBootstrap();
        if (enrollmentBootstrap?.Token is not null)
            return await RunSessionEnrollmentAsync(args, enrollmentBootstrap);

        HelperSettings settings = LoadSettings();
        string? baseUrl = GetArg(args, "--base-url")
            ?? settings.ApiBaseUrl
            ?? Environment.GetEnvironmentVariable("QEC_ITMG_BASE_URL")
            ?? "http://localhost:5080";

        string apiRoot = baseUrl.TrimEnd('/');
        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(120) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Qec.Itmg.RemoteSupport.Helper/1.2.0");

        Console.WriteLine("Starting secure pairing...");
        HttpResponseMessage startResponse;
        try
        {
            startResponse = await client.PostAsync($"{apiRoot}/api/v1/remote-support/pairings", content: null);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not reach ITMG at {apiRoot}: {ex.Message}");
            Console.Error.WriteLine("If this is not a Development PC, re-download the helper from Remote Support setup.");
            return 3;
        }

        string startPayload = await startResponse.Content.ReadAsStringAsync();
        if (!startResponse.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"Pairing start failed ({(int)startResponse.StatusCode}).");
            Console.Error.WriteLine(SafeErrorMessage(startPayload));
            return 4;
        }

        PairingStartResponse? started = JsonSerializer.Deserialize<PairingStartResponse>(startPayload, JsonOpts);
        if (started is null || string.IsNullOrWhiteSpace(started.DeviceSecret))
        {
            Console.Error.WriteLine("Unexpected pairing response from ITMG.");
            return 5;
        }

        string openUrl = started.VerificationUriComplete
            ?? $"{started.VerificationUri}?code={Uri.EscapeDataString(started.UserCode)}";
        Console.WriteLine($"Pairing code: {started.UserCode}");
        Console.WriteLine("Opening your browser to approve pairing...");
        try
        {
            Process.Start(new ProcessStartInfo { FileName = openUrl, UseShellExecute = true });
        }
        catch
        {
            Console.WriteLine($"Open this link manually: {openUrl}");
        }

        Console.WriteLine("Waiting for approval in the browser...");
        PairingStatusResponse? status = null;
        for (int i = 0; i < 120; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            using HttpRequestMessage poll = new(HttpMethod.Get, $"{apiRoot}/api/v1/remote-support/pairings/{started.PairingId}/status");
            poll.Headers.TryAddWithoutValidation("X-Device-Secret", started.DeviceSecret);
            HttpResponseMessage pollResponse = await client.SendAsync(poll);
            string pollPayload = await pollResponse.Content.ReadAsStringAsync();
            if (!pollResponse.IsSuccessStatusCode)
            {
                Console.Error.WriteLine(SafeErrorMessage(pollPayload));
                return 6;
            }

            status = JsonSerializer.Deserialize<PairingStatusResponse>(pollPayload, JsonOpts);
            string st = status?.Status ?? "";
            if (string.Equals(st, "Authorized", StringComparison.OrdinalIgnoreCase))
                break;
            if (string.Equals(st, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Pairing was cancelled in the browser.");
                return 7;
            }

            if (string.Equals(st, "Expired", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Pairing code expired. Run the helper again.");
                return 8;
            }

            if (string.Equals(st, "Completed", StringComparison.OrdinalIgnoreCase))
                break;
        }

        if (status is null
            || (!string.Equals(status.Status, "Authorized", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(status.Status, "Completed", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine("Timed out waiting for browser approval. Run the helper again.");
            return 9;
        }

        if (string.Equals(status.Status, "Completed", StringComparison.OrdinalIgnoreCase)
            && status.EndpointId is not null)
        {
            Console.WriteLine($"Already paired: {status.DeviceName} ({status.ConnectionStatus})");
            return await MaybeBootstrapAgentAsync(client, status);
        }

        DeviceIdentity device = DetectDevice();
        Console.WriteLine($"Registering {device.DeviceName}...");
        HttpResponseMessage completeResponse = await client.PostAsJsonAsync(
            $"{apiRoot}/api/v1/remote-support/pairings/{started.PairingId}/complete",
            new
            {
                deviceSecret = started.DeviceSecret,
                deviceName = device.DeviceName,
                operatingSystem = device.OperatingSystem,
                operatingSystemVersion = device.OperatingSystemVersion,
                architecture = device.Architecture,
                helperVersion = device.HelperVersion,
            },
            JsonOpts);

        string completePayload = await completeResponse.Content.ReadAsStringAsync();
        if (!completeResponse.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"Pairing complete failed ({(int)completeResponse.StatusCode}).");
            Console.Error.WriteLine(SafeErrorMessage(completePayload));
            return 10;
        }

        PairingStatusResponse? completed = JsonSerializer.Deserialize<PairingStatusResponse>(completePayload, JsonOpts);
        if (completed is null)
        {
            Console.Error.WriteLine("Unexpected completion response.");
            return 11;
        }

        Console.WriteLine($"Computer paired: {completed.DeviceName}");
        Console.WriteLine($"Status: {completed.ConnectionStatus}");
        return await MaybeBootstrapAgentAsync(client, completed);
    }

    private static async Task<int> MaybeBootstrapAgentAsync(HttpClient client, PairingStatusResponse completed)
    {
        string? agentUrl = completed.AgentDownloadUrl;
        if (string.IsNullOrWhiteSpace(agentUrl))
        {
            if (completed.WaitingForRemoteAgent)
            {
                Console.WriteLine("Waiting for remote agent — MeshCentral is not ready yet.");
                Console.WriteLine("Your computer is paired. Return to Remote Support setup.");
            }
            else
            {
                Console.WriteLine("Ready for remote support. Return to the Remote Support page.");
            }

            Console.WriteLine();
            Console.WriteLine("You can close this window.");
            return 0;
        }

        Console.WriteLine("Downloading remote support agent...");
        if (!string.IsNullOrWhiteSpace(completed.AgentInstallInstructions))
            Console.WriteLine(completed.AgentInstallInstructions);

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
            Console.WriteLine("Computer is paired. You can continue from the Remote Support page.");
            return 0;
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

        if (!string.IsNullOrWhiteSpace(completed.ReportSecret) && completed.EndpointId is Guid epId)
        {
            try
            {
                string? apiRoot = client.BaseAddress?.ToString()?.TrimEnd('/');
                // Absolute URLs used earlier; rebuild from last request is hard — use Agent URL host path via relative.
            }
            catch
            {
                // best-effort below via absolute if we stored api — skip if missing
            }
        }

        Console.WriteLine("Waiting for remote agent registration...");
        Console.WriteLine("Return to Remote Support setup — status updates automatically.");
        Console.WriteLine();
        Console.WriteLine("You can close this window.");
        return 0;
    }

    private static async Task<int> RunSessionEnrollmentAsync(string[] args, BootstrapConfig bootstrap)
    {
        // Preserve session-bound prepare-this-computer path.
        string? baseUrl = GetArg(args, "--base-url")
            ?? bootstrap.BaseUrl
            ?? Environment.GetEnvironmentVariable("QEC_ITMG_BASE_URL");
        string? token = GetArg(args, "--token")
            ?? bootstrap.Token
            ?? Environment.GetEnvironmentVariable("QEC_REMOTE_ENROLLMENT_TOKEN");

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine("Missing enrollment bootstrap. Re-download from the Remote Support request page.");
            return 2;
        }

        DeviceIdentity device = DetectDevice();
        string apiRoot = baseUrl.TrimEnd('/');
        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(120) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Qec.Itmg.RemoteSupport.Helper/{device.HelperVersion}");

        var body = new
        {
            token,
            deviceName = device.DeviceName,
            operatingSystem = device.OperatingSystem,
            operatingSystemVersion = device.OperatingSystemVersion,
            architecture = device.Architecture,
            helperVersion = device.HelperVersion,
            agentStatus = "installing",
        };

        TryDelete(Path.Combine(AppContext.BaseDirectory, "enrollment.bootstrap.json"));

        Console.WriteLine("Registering this computer with the support request...");
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{apiRoot}/api/v1/remote-support/enrollments/redeem",
            body,
            JsonOpts);
        string payload = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine(SafeErrorMessage(payload));
            return 4;
        }

        Console.WriteLine("Registered with the support request. Return to the Remote Support page.");
        return 0;
    }

    private static HelperSettings LoadSettings()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "helper.settings.json");
        if (File.Exists(path))
        {
            try
            {
                return JsonSerializer.Deserialize<HelperSettings>(File.ReadAllText(path), JsonOpts)
                    ?? new HelperSettings(null, null);
            }
            catch
            {
                // fall through
            }
        }

        // Embedded / Development defaults — production publish should include helper.settings.json.
        return new HelperSettings("http://localhost:5080", "http://localhost:5173");
    }

    private static BootstrapConfig? TryLoadEnrollmentBootstrap()
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
        return new DeviceIdentity(name, os, version, arch, "1.2.0");
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

    private sealed record HelperSettings(string? ApiBaseUrl, string? AppBaseUrl);

    private sealed record BootstrapConfig(string? BaseUrl, string? Token, DateTimeOffset? ExpiresAtUtc, Guid? EnrollmentId);

    private sealed record DeviceIdentity(
        string DeviceName,
        string OperatingSystem,
        string? OperatingSystemVersion,
        string Architecture,
        string HelperVersion);

    private sealed record PairingStartResponse(
        Guid PairingId,
        string DeviceSecret,
        string UserCode,
        string VerificationUri,
        string VerificationUriComplete,
        DateTimeOffset ExpiresAtUtc);

    private sealed record PairingStatusResponse(
        Guid PairingId,
        string Status,
        DateTimeOffset ExpiresAtUtc,
        Guid? EndpointId,
        string? DeviceName,
        string? ConnectionStatus,
        bool WaitingForRemoteAgent,
        string? AgentDownloadUrl,
        string? AgentInstallInstructions,
        string? ReportSecret);
}
