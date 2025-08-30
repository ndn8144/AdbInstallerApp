using AdbInstallerApp.Models;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AdbInstallerApp.Helpers;

namespace AdbInstallerApp.Services;

public interface IApkAnalyzerService
{
    Task<ApkFile?> AnalyzeApkAsync(string apkPath, CancellationToken ct = default);
    Task<List<ApkFile>> AnalyzeApkGroupAsync(IEnumerable<string> apkPaths, CancellationToken ct = default);
    Task<ApkManifestInfo> ExtractManifestInfoAsync(string apkPath, CancellationToken ct = default);
}

public sealed class ApkAnalyzerService : IApkAnalyzerService
{
    private readonly ILogBus _logBus;
    private readonly string _aaptPath;
    private readonly string _apkanalyzerPath;
    private readonly bool _useAapt;

    public ApkAnalyzerService(ILogBus logBus)
    {
        _logBus = logBus;
        
        // Try to find aapt/apkanalyzer in platform-tools
        var baseDir = AppContext.BaseDirectory;
        var toolsDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "tools", "platform-tools"));
        
        var aaptCandidate = Path.Combine(toolsDir, "aapt.exe");
        var apkanalyzerCandidate = Path.Combine(toolsDir, "apkanalyzer.exe");
        
        if (File.Exists(apkanalyzerCandidate))
        {
            _apkanalyzerPath = apkanalyzerCandidate;
            _useAapt = false;
            _logBus.WriteDebug($"Using apkanalyzer: {_apkanalyzerPath}");
        }
        else if (File.Exists(aaptCandidate))
        {
            _aaptPath = aaptCandidate;
            _useAapt = true;
            _logBus.WriteDebug($"Using aapt: {_aaptPath}");
        }
        else
        {
            // Fallback to system PATH
            _apkanalyzerPath = "apkanalyzer";
            _useAapt = false;
            _logBus.WriteWarning("Using system PATH apkanalyzer (may not be available)");
        }
    }

    public async Task<ApkFile?> AnalyzeApkAsync(string apkPath, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(apkPath))
            {
                _logBus.WriteError($"APK file not found: {apkPath}");
                return null;
            }

            var fileInfo = new FileInfo(apkPath);
            var manifestInfo = await ExtractManifestInfoAsync(apkPath, ct);
            
            if (manifestInfo == null)
            {
                _logBus.WriteWarning($"Could not extract manifest info from: {Path.GetFileName(apkPath)}");
                return null;
            }

            // Calculate SHA256
            var sha256 = await CalculateSha256Async(apkPath, ct);
            
            var apkFile = new ApkFile(
                Path: apkPath,
                PackageName: manifestInfo.PackageName,
                IsBase: !IsSplitApk(apkPath),
                Abi: ExtractAbiFromPath(apkPath),
                Dpi: ExtractDpiFromPath(apkPath),
                Locale: ExtractLocaleFromPath(apkPath),
                SplitName: IsSplitApk(apkPath) ? ExtractSplitName(apkPath) : null,
                VersionCode: manifestInfo.VersionCode,
                Sha256: sha256,
                SizeBytes: fileInfo.Length,
                SignerDigest: null, // Would need signature verification
                MinSdk: manifestInfo.MinSdkVersion,
                TargetSdk: manifestInfo.TargetSdkVersion
            );

            _logBus.WriteDebug($"Analyzed APK: {Path.GetFileName(apkPath)} - Package: {apkFile.PackageName}, Size: {FormatBytes(apkFile.SizeBytes)}");
            return apkFile;
        }
        catch (Exception ex)
        {
            _logBus.WriteError($"Failed to analyze APK {Path.GetFileName(apkPath)}: {ex.Message}");
            return null;
        }
    }

    public async Task<List<ApkFile>> AnalyzeApkGroupAsync(IEnumerable<string> apkPaths, CancellationToken ct = default)
    {
        var apkFiles = new List<ApkFile>();
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount);

        var tasks = apkPaths.Select(async path =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                return await AnalyzeApkAsync(path, ct);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        apkFiles.AddRange(results.Where(a => a != null)!);

        return apkFiles;
    }

    public async Task<ApkManifestInfo> ExtractManifestInfoAsync(string apkPath, CancellationToken ct = default)
    {
        try
        {
            if (_useAapt)
            {
                return await ExtractManifestWithAaptAsync(apkPath, ct);
            }
            else
            {
                return await ExtractManifestWithApkanalyzerAsync(apkPath, ct);
            }
        }
        catch (Exception ex)
        {
            _logBus.WriteError($"Failed to extract manifest info: {ex.Message}");
            return ApkManifestInfo.Empty;
        }
    }

    private async Task<ApkManifestInfo> ExtractManifestWithAaptAsync(string apkPath, CancellationToken ct)
    {
        var args = $"dump badging \"{apkPath}\"";
        var result = await Proc.RunAsync(_aaptPath, args, ct: ct);
        
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"aapt failed: {result.StdErr}");
        }

        return ParseAaptOutput(result.StdOut);
    }

    private async Task<ApkManifestInfo> ExtractManifestWithApkanalyzerAsync(string apkPath, CancellationToken ct)
    {
        var args = $"manifest \"{apkPath}\"";
        var result = await Proc.RunAsync(_apkanalyzerPath, args, ct: ct);
        
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"apkanalyzer failed: {result.StdErr}");
        }

        return ParseApkanalyzerOutput(result.StdOut);
    }

    private ApkManifestInfo ParseAaptOutput(string output)
    {
        var packageName = "";
        var versionCode = 0L;
        var minSdkVersion = 21;
        var targetSdkVersion = 34;

        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("package:"))
            {
                var match = Regex.Match(line, @"package: name='([^']+)' code='(\d+)'");
                if (match.Success)
                {
                    packageName = match.Groups[1].Value;
                    long.TryParse(match.Groups[2].Value, out versionCode);
                }
            }
            else if (line.StartsWith("sdkVersion:"))
            {
                var match = Regex.Match(line, @"sdkVersion:'(\d+)'");
                if (match.Success)
                {
                    int.TryParse(match.Groups[1].Value, out minSdkVersion);
                }
            }
            else if (line.StartsWith("targetSdkVersion:"))
            {
                var match = Regex.Match(line, @"targetSdkVersion:'(\d+)'");
                if (match.Success)
                {
                    int.TryParse(match.Groups[1].Value, out targetSdkVersion);
                }
            }
        }

        return new ApkManifestInfo(packageName, versionCode, minSdkVersion, targetSdkVersion);
    }

    private ApkManifestInfo ParseApkanalyzerOutput(string output)
    {
        // apkanalyzer output is XML, so we need to parse it differently
        // For now, return basic info - in production, use proper XML parsing
        return new ApkManifestInfo("", 0, 21, 34);
    }

    private async Task<string> CalculateSha256Async(string filePath, CancellationToken ct)
    {
        using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private bool IsSplitApk(string apkPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(apkPath).ToLowerInvariant();
        return fileName.Contains("split_") || fileName.Contains("config.");
    }

    private string? ExtractAbiFromPath(string apkPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(apkPath).ToLowerInvariant();
        var abiMatch = Regex.Match(fileName, @"(arm64-v8a|armeabi-v7a|armeabi|x86|x86_64)");
        return abiMatch.Success ? abiMatch.Groups[1].Value : null;
    }

    private string? ExtractDpiFromPath(string apkPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(apkPath).ToLowerInvariant();
        var dpiMatch = Regex.Match(fileName, @"(ldpi|mdpi|hdpi|xhdpi|xxhdpi|xxxhdpi|\d+dpi)");
        return dpiMatch.Success ? dpiMatch.Groups[1].Value : null;
    }

    private string? ExtractLocaleFromPath(string apkPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(apkPath).ToLowerInvariant();
        var localeMatch = Regex.Match(fileName, @"([a-z]{2}(?:-[A-Z]{2})?)");
        return localeMatch.Success ? localeMatch.Groups[1].Value : null;
    }

    private string? ExtractSplitName(string apkPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(apkPath).ToLowerInvariant();
        if (fileName.Contains("split_"))
        {
            var splitMatch = Regex.Match(fileName, @"split_([^.]+)");
            return splitMatch.Success ? splitMatch.Groups[1].Value : null;
        }
        else if (fileName.Contains("config."))
        {
            var configMatch = Regex.Match(fileName, @"config\.([^.]+)");
            return configMatch.Success ? configMatch.Groups[1].Value : null;
        }
        return null;
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

public record ApkManifestInfo(
    string PackageName,
    long VersionCode,
    int MinSdkVersion,
    int TargetSdkVersion)
{
    public static ApkManifestInfo Empty => new("", 0, 21, 34);
}
