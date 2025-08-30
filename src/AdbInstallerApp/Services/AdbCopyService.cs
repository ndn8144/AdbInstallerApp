using AdbInstallerApp.Utils;

namespace AdbInstallerApp.Services;

public record CopyOptions(
    string TargetDir = "/sdcard/Download/APKsInstaller",
    bool Overwrite = false, 
    int MaxRetries = 1);

public interface IAdbCopyService
{
    Task CopyFilesAsync(string serial, IReadOnlyList<string> hostPaths,
                        string packageName, CopyOptions opt,
                        IProgress<(string file, long deltaBytes)> progress,
                        IProgress<string> log, CancellationToken ct);
}

public sealed class AdbCopyService : IAdbCopyService
{
    private readonly string _adbPath;

    public AdbCopyService(string adbPath) => _adbPath = adbPath;

    public async Task CopyFilesAsync(string serial, IReadOnlyList<string> hostPaths,
        string packageName, CopyOptions opt,
        IProgress<(string file, long deltaBytes)> progress, 
        IProgress<string> log, CancellationToken ct)
    {
        // Create destination directory
        var dest = $"{opt.TargetDir.TrimEnd('/')}/{packageName}";
        await Proc.RunAsync(_adbPath, $"-s {serial} shell mkdir -p \"{dest}\"", null, log, ct)
            .ConfigureAwait(false);

        foreach (var path in hostPaths)
        {
            ct.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(path);
            var devicePath = $"{dest}/{fileName}".Replace("\\", "/");

            if (!opt.Overwrite)
            {
                // If exists with same size → skip
                var size = new FileInfo(path).Length;
                var stat = await Proc.RunAsync(_adbPath, $"-s {serial} shell ls -l \"{devicePath}\"", null, null, ct)
                    .ConfigureAwait(false);
                if (stat.ExitCode == 0 && TryParseSize(stat.StdOut, out var existing) && existing == size)
                {
                    log?.Report($"Skip (exists): {devicePath}");
                    continue;
                }
            }

            log?.Report($"PUSH {fileName} → {devicePath}");
            await PushWithProgress(serial, path, devicePath, progress, ct).ConfigureAwait(false);

            // Verify size
            var verify = await Proc.RunAsync(_adbPath, $"-s {serial} shell ls -l \"{devicePath}\"", null, null, ct)
                .ConfigureAwait(false);
            if (verify.ExitCode != 0 || !TryParseSize(verify.StdOut, out var got) || got != new FileInfo(path).Length)
            {
                if (opt.MaxRetries > 0)
                {
                    log?.Report("Mismatch, retry once…");
                    await PushWithProgress(serial, path, devicePath, progress, ct).ConfigureAwait(false);
                }
                else
                {
                    throw new InvalidOperationException($"Verify failed: {devicePath}");
                }
            }
        }
    }

    private static bool TryParseSize(string lsOut, out long size)
    {
        // Format: -rw-rw---- 1 u0_a123 sdcard_rw 123456 2024-01-01 12:34 file.apk
        size = 0;
        var parts = lsOut.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 5 && long.TryParse(parts[4], out size);
    }

    private async Task PushWithProgress(string serial, string host, string device,
                                        IProgress<(string file, long delta)>? progress, CancellationToken ct)
    {
        // Use adb push with progress reporting
        var quotedHost = Proc.QuotePath(host);
        var res = await Proc.RunAsync(_adbPath, $"-s {serial} push {quotedHost} \"{device}\"", null,
                                      new Progress<string>(line => progress?.Report((host, 0))), ct)
            .ConfigureAwait(false);
        
        if (res.ExitCode != 0) 
            throw new InvalidOperationException($"Push failed: {res.StdErr}");
    }
}
