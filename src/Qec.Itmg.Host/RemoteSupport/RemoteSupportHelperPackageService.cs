using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Qec.Itmg.RemoteSupport;
using Qec.Itmg.RemoteSupport.Services;

namespace Qec.Itmg.Host.RemoteSupport;

/// <summary>
/// Assembles a session-bound helper package (EXE + short-lived enrollment bootstrap).
/// Does not rebuild the EXE per request — packs the published artifact with a one-time token file.
/// </summary>
public sealed class RemoteSupportHelperPackageService(IOptions<RemoteSupportOptions> options)
{
    public string? ResolveHelperExePath()
    {
        RemoteSupportOptions cfg = options.Value;
        if (!string.IsNullOrWhiteSpace(cfg.HelperArtifactPath))
        {
            string? configured = ResolveFromPath(cfg.HelperArtifactPath.Trim());
            if (configured is not null)
                return configured;
        }

        // Development convenience: repo artifacts/ folder relative to content roots.
        string[] candidates =
        [
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "remote-support")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "remote-support")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "artifacts", "remote-support")),
        ];
        foreach (string dir in candidates)
        {
            string? found = ResolveFromPath(dir);
            if (found is not null)
                return found;
        }

        return null;
    }

    public bool IsExeAvailable => ResolveHelperExePath() is not null;

    public bool IsAvailable => IsExeAvailable || options.Value.HasHelperDownload;

    public async Task<(byte[] Content, string FileName)> BuildPackageAsync(
        EnrollmentIssueResult enrollment,
        string publicAppBaseUrl,
        CancellationToken cancellationToken)
    {
        RemoteSupportOptions cfg = options.Value;
        string? helperPath = ResolveHelperExePath();
        if (helperPath is null)
            throw new InvalidOperationException("Support Helper is not available on this environment.");

        string appBase = string.IsNullOrWhiteSpace(publicAppBaseUrl)
            ? (string.IsNullOrWhiteSpace(cfg.PublicAppBaseUrl) ? "" : cfg.PublicAppBaseUrl.TrimEnd('/'))
            : publicAppBaseUrl.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(appBase))
            throw new InvalidOperationException("PublicAppBaseUrl is not configured for helper bootstrap.");

        var bootstrap = new
        {
            baseUrl = appBase,
            token = enrollment.Token,
            expiresAtUtc = enrollment.ExpiresAtUtc,
            enrollmentId = enrollment.EnrollmentId,
        };

        await using MemoryStream zipStream = new();
        using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry exeEntry = archive.CreateEntry("QecRemoteSupportHelper.exe", CompressionLevel.Optimal);
            await using (Stream entryStream = exeEntry.Open())
            await using (FileStream file = File.OpenRead(helperPath))
                await file.CopyToAsync(entryStream, cancellationToken);

            ZipArchiveEntry cfgEntry = archive.CreateEntry("enrollment.bootstrap.json", CompressionLevel.Optimal);
            await using (Stream entryStream = cfgEntry.Open())
            {
                byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(bootstrap));
                await entryStream.WriteAsync(json, cancellationToken);
            }

            ZipArchiveEntry readme = archive.CreateEntry("README.txt", CompressionLevel.Optimal);
            await using (Stream entryStream = readme.Open())
            {
                byte[] text = Encoding.UTF8.GetBytes(
                    "QEC Remote Support Helper\r\n\r\n" +
                    "1. Extract this zip.\r\n" +
                    "2. Run QecRemoteSupportHelper.exe\r\n" +
                    "3. Return to the Remote Support page — device status updates automatically.\r\n\r\n" +
                    "Do not share this package. The enrollment file expires quickly and is single-use.\r\n");
                await entryStream.WriteAsync(text, cancellationToken);
            }
        }

        return (zipStream.ToArray(), "QecRemoteSupportHelper.zip");
    }

    private static string? ResolveFromPath(string path)
    {
        if (File.Exists(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return path;
        if (Directory.Exists(path))
        {
            return Directory.EnumerateFiles(path, "QecRemoteSupportHelper.exe", SearchOption.AllDirectories)
                    .FirstOrDefault()
                ?? Directory.EnumerateFiles(path, "Qec.Itmg.RemoteSupport.Helper.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();
        }

        return null;
    }
}
