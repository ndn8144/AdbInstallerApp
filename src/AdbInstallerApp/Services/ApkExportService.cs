using AdbInstallerApp.Helpers;
using AdbInstallerApp.Models;
using AdbInstallerApp.Services;
using System.Text.RegularExpressions;

namespace AdbInstallerApp.Services
{
    public class ApkExportService
    {
        private readonly AdbService _adb;
        private readonly ILogBus? _logBus; // Make nullable for backward compatibility

        public ApkExportService(AdbService adb, ILogBus? logBus = null)
        {
            _adb = adb;
            _logBus = logBus;
        }

        public async Task<ExportResult> ExportApkAsync(
            string serial,
            string packageName,
            string destDir,
            bool includeSplits,
            IProgress<TransferProgress>? progress,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(serial))
                throw new ArgumentException("Serial cannot be null or empty", nameof(serial));

            if (string.IsNullOrEmpty(packageName))
                throw new ArgumentException("Package name cannot be null or empty", nameof(packageName));

            if (string.IsNullOrEmpty(destDir))
                throw new ArgumentException("Destination directory cannot be null or empty", nameof(destDir));

            try
            {
                Directory.CreateDirectory(destDir);

                // Get APK paths on device
                var paths = await GetPackagePathsAsync(serial, packageName, ct);
                if (paths.Count == 0)
                {
                    return new ExportResult(packageName, Array.Empty<string>(), false,
                        $"No APK paths found for package {packageName}");
                }

                // Filter paths if not including splits
                var pathsToExport = includeSplits ? paths : paths.Take(1).ToList();

                // Create package-specific directory
                var pkgFolder = Path.Combine(destDir, SanitizeFileName(packageName));
                Directory.CreateDirectory(pkgFolder);

                var exportedPaths = new List<string>();

                // Export each APK file
                for (int i = 0; i < pathsToExport.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var remotePath = pathsToExport[i];
                    var localFileName = InferLocalApkName(remotePath, i, packageName);
                    var localPath = Path.Combine(pkgFolder, localFileName);

                    progress?.Report(new TransferProgress(remotePath, localPath, 0, 100, 0));

                    var success = await PullApkAsync(serial, remotePath, localPath, progress, ct);

                    if (success && File.Exists(localPath))
                    {
                        exportedPaths.Add(localPath);
                        progress?.Report(new TransferProgress(remotePath, localPath, 100, 100, 100));
                    }
                    else
                    {
                        // Try alternative methods for protected apps
                        var altSuccess = await TryAlternativePullAsync(serial, remotePath, localPath, ct);
                        if (altSuccess && File.Exists(localPath))
                        {
                            exportedPaths.Add(localPath);
                        }
                        else
                        {
                            return new ExportResult(packageName, exportedPaths.ToList(), false,
                                $"Failed to export {remotePath}. Possible permission denied.");
                        }
                    }
                }

                return new ExportResult(packageName, exportedPaths.ToList(), true);
            }
            catch (Exception ex)
            {
                return new ExportResult(packageName, Array.Empty<string>(), false, ex.Message);
            }
        }

        public async Task<ExportResult> ExportMultipleApksAsync(
            string serial,
            IEnumerable<string> packageNames,
            string destDir,
            bool includeSplits,
            IProgress<string>? progressLog,
            CancellationToken ct = default)
        {
            var allExportedPaths = new List<string>();
            var errors = new List<string>();
            var packages = packageNames.ToList();

            progressLog?.Report($"Starting export of {packages.Count} package(s)...");

            for (int i = 0; i < packages.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var packageName = packages[i];

                progressLog?.Report($"[{i + 1}/{packages.Count}] Exporting {packageName}...");

                var result = await ExportApkAsync(serial, packageName, destDir, includeSplits, null, ct);

                if (result.Success)
                {
                    allExportedPaths.AddRange(result.ExportedPaths);
                    progressLog?.Report($"✅ {packageName}: {result.ExportedPaths.Count} file(s) exported");
                }
                else
                {
                    errors.Add($"{packageName}: {result.ErrorMessage}");
                    progressLog?.Report($"❌ {packageName}: {result.ErrorMessage}");
                }
            }

            var finalSuccess = allExportedPaths.Count > 0;
            var finalMessage = errors.Count > 0 ? string.Join("; ", errors) : null;

            progressLog?.Report($"Export completed. {allExportedPaths.Count} file(s) exported successfully.");

            return new ExportResult("Multiple", allExportedPaths, finalSuccess, finalMessage);
        }

        private async Task<List<string>> GetPackagePathsAsync(string serial, string packageName, CancellationToken ct)
        {
            var cmd = $"-s {serial} shell pm path {packageName}";
            var result = await Proc.RunAsync(_adb.AdbPath, cmd, null, _logBus != null ? new Progress<string>(_logBus.Write) : null, ct);
            var output = result.StdOut;

            return ParsePmPath(output);
        }

        private List<string> ParsePmPath(string output)
        {
            var paths = new List<string>();
            if (string.IsNullOrWhiteSpace(output)) return paths;

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("package:"))
                {
                    paths.Add(trimmed.Substring("package:".Length));
                }
            }

            return paths;
        }

        private async Task<bool> PullApkAsync(
            string serial,
            string remotePath,
            string localPath,
            IProgress<TransferProgress>? progress,
            CancellationToken ct)
        {
            try
            {
                // Use -p flag for progress
                var cmd = $"-s {serial} pull -p \"{remotePath}\" \"{localPath}\"";
                var result = await Proc.RunAsync(_adb.AdbPath, cmd, null, _logBus != null ? new Progress<string>(_logBus.Write) : null, ct);
                var exitCode = result.ExitCode;
                var stdout = result.StdOut;
                var stderr = result.StdErr;

                // Update progress from stderr output
                if (progress != null)
                {
                    var transferProgress = TransferProgress.FromAdb(stderr, remotePath, localPath);
                    progress.Report(transferProgress);
                }

                return exitCode == 0 && File.Exists(localPath) && new FileInfo(localPath).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TryAlternativePullAsync(
            string serial,
            string remotePath,
            string localPath,
            CancellationToken ct)
        {
            // Try run-as method for debuggable apps (limited use case)
            // This is a fallback that rarely works but worth trying

            try
            {
                var tempPath = $"/sdcard/temp_export_{Path.GetFileName(localPath)}";

                // Try to copy to sdcard first (if we have permission)
                var copyCmd = $"-s {serial} shell cp \"{remotePath}\" \"{tempPath}\"";
                var copyResult = await Proc.RunAsync(_adb.AdbPath, copyCmd, null, _logBus != null ? new Progress<string>(_logBus.Write) : null, ct);
                var copyCode = copyResult.ExitCode;

                if (copyCode == 0)
                {
                    // Pull from sdcard
                    var pullCmd = $"-s {serial} pull \"{tempPath}\" \"{localPath}\"";
                    var pullResult = await Proc.RunAsync(_adb.AdbPath, pullCmd, null, _logBus != null ? new Progress<string>(_logBus.Write) : null, ct);
                    var pullCode = pullResult.ExitCode;

                    // Cleanup
                    _ = Proc.RunAsync(_adb.AdbPath, $"-s {serial} shell rm \"{tempPath}\"", null, _logBus != null ? new Progress<string>(_logBus.Write) : null, ct);

                    return pullCode == 0 && File.Exists(localPath) && new FileInfo(localPath).Length > 0;
                }
            }
            catch
            {
                // Ignore errors in alternative method
            }

            return false;
        }

        private static string InferLocalApkName(string remotePath, int index, string packageName)
        {
            var fileName = Path.GetFileName(remotePath);

            // If it's clearly a split APK
            if (fileName.Contains("split_") || fileName.Contains("config."))
            {
                return fileName; // Keep original split name
            }

            // For base APK or unclear names
            if (index == 0)
            {
                return $"{packageName}_base.apk";
            }
            else
            {
                return $"{packageName}_split_{index}.apk";
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            // Remove invalid filename characters
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }
    }
}