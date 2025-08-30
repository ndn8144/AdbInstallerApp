
using AdbInstallerApp.Models;
using AdbInstallerApp.Helpers;
using AdbInstallerApp.Services;
using System.Text.RegularExpressions;

namespace AdbInstallerApp.Services
{
    public class AppInventoryService
    {
        private readonly AdbService _adb;
        private readonly ILogBus? _logBus; // Make nullable for backward compatibility

        public AppInventoryService(AdbService adb, ILogBus? logBus = null)
        {
            _adb = adb;
            _logBus = logBus;
        }

        public async Task<IReadOnlyList<InstalledApp>> ListInstalledAppsAsync(
            string serial,
            AppQueryOptions opts,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(serial))
                throw new ArgumentException("Serial cannot be null or empty", nameof(serial));

            try
            {
                // 1) List packages with optimized command
                var packages = await ListPackagesOptimizedAsync(serial, opts, ct);

                if (packages.Count == 0)
                    return Array.Empty<InstalledApp>();

                // 2) Batch process packages for better performance
                var results = new List<InstalledApp>();
                var semaphore = new SemaphoreSlim(6); // Increased concurrency
                var batchSize = 10;

                for (int i = 0; i < packages.Count; i += batchSize)
                {
                    var batch = packages.Skip(i).Take(batchSize);
                    var batchTasks = batch.Select(async pkg =>
                    {
                        await semaphore.WaitAsync(ct);
                        try
                        {
                            return await EnrichPackageInfoOptimizedAsync(serial, pkg, opts.WithSizes, ct);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    var batchResults = await Task.WhenAll(batchTasks);
                    var validApps = batchResults.Where(app => app != null).Cast<InstalledApp>();
                
                    // Deduplicate within batch using HashSet for O(1) lookup
                    var existingPackages = new HashSet<string>(results.Select(r => r.PackageName), StringComparer.OrdinalIgnoreCase);
                    var uniqueApps = validApps.Where(app => !existingPackages.Contains(app.PackageName)).ToList();
                
                    results.AddRange(uniqueApps);
                
                    // Allow UI updates between batches
                    await Task.Delay(1, ct);
                }

                // 3) Apply filters
                return ApplyFilters(results, opts);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to list installed apps for {serial}: {ex.Message}", ex);
            }
        }

        private async Task<List<PackageEntry>> ListPackagesOptimizedAsync(string serial, AppQueryOptions opts, CancellationToken ct)
        {
            // Always get all packages first, then filter in code for reliability
            var cmd = $"-s {serial} shell cmd package list packages -f --user {opts.UserId}";
            var result = await Proc.RunAsync(_adb.AdbPath, cmd, null, _logBus != null ? new Progress<string>(_logBus.Write) : null, ct);
            var code = result.ExitCode;
            var output = result.StdOut;

            if (code != 0 || string.IsNullOrWhiteSpace(output))
            {
                // Fallback to pm command
                cmd = $"-s {serial} shell pm list packages -f --user {opts.UserId}";
                result = await Proc.RunAsync(_adb.AdbPath, cmd, null, _logBus != null ? new Progress<string>(_logBus.Write) : null, ct);
                output = result.StdOut;
                
                if (string.IsNullOrWhiteSpace(output))
                {
                    // Try without --user flag as fallback
                    cmd = $"-s {serial} shell pm list packages -f";
                    result = await Proc.RunAsync(_adb.AdbPath, cmd, null, _logBus != null ? new Progress<string>(_logBus.Write) : null, ct);
                    output = result.StdOut;
                }
            }

            return ParseListPackages(output);
        }

        private List<PackageEntry> ParseListPackages(string output)
        {
            var packages = new List<PackageEntry>();
            if (string.IsNullOrWhiteSpace(output)) return packages;

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Pattern: package:/data/app/.../base.apk=com.example.app
            var regex = new Regex(@"package:(.+?)=([a-zA-Z0-9_.]+)", RegexOptions.Compiled);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var match = regex.Match(trimmed);
                if (match.Success)
                {
                    var path = match.Groups[1].Value;
                    var package = match.Groups[2].Value;

                    packages.Add(new PackageEntry(package, path));
                }
            }

            return packages;
        }

        private async Task<InstalledApp?> EnrichPackageInfoOptimizedAsync(
            string serial,
            PackageEntry pkg,
            bool withSizes,
            CancellationToken ct)
        {
            try
            {
                // Run operations in parallel for better performance
                var pathsTask = GetPackagePathsAsync(serial, pkg.PackageName, ct);
                var detailsTask = GetPackageDetailsOptimizedAsync(serial, pkg.PackageName, ct);

                await Task.WhenAll(pathsTask, detailsTask);

                var paths = await pathsTask;
                var (label, versionName, versionCode, isSystem) = await detailsTask;

                // Get size if requested (skip for system apps to improve performance)
                long? totalSize = null;
                if (withSizes && !isSystem)
                {
                    totalSize = await CalculatePackageSizeOptimizedAsync(serial, paths, ct);
                }

                return new InstalledApp(
                    pkg.PackageName,
                    label,
                    versionName,
                    versionCode,
                    0, // UserId - could be enhanced later
                    isSystem,
                    paths,
                    totalSize
                );
            }
            catch
            {
                // If we can't get details for a package, skip it
                return null;
            }
        }

        private async Task<IReadOnlyList<string>> GetPackagePathsAsync(
            string serial,
            string packageName,
            CancellationToken ct)
        {
            var cmd = $"-s {serial} shell pm path {packageName}";
            var result = await Proc.RunAsync(_adb.AdbPath, cmd, null, _logBus != null ? new Progress<string>(_logBus.Write) : null, ct);
            var code = result.ExitCode;
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

        private async Task<(string label, string versionName, long versionCode, bool isSystem)>
            GetPackageDetailsOptimizedAsync(string serial, string packageName, CancellationToken ct)
        {
            // Use lighter weight command for basic info
            var cmd = $"-s {serial} shell dumpsys package {packageName} | grep -E 'applicationLabel=|versionName=|versionCode=|flags='";
            var result = await Proc.RunAsync(_adb.AdbPath, cmd, null, _logBus != null ? new Progress<string>(_logBus.Write) : null, ct);
            var code = result.ExitCode;
            var output = result.StdOut;

            if (code != 0 || string.IsNullOrWhiteSpace(output))
            {
                // Fallback to full dumpsys
                cmd = $"-s {serial} shell dumpsys package {packageName}";
                result = await Proc.RunAsync(_adb.AdbPath, cmd, null, _logBus != null ? new Progress<string>(_logBus.Write) : null, ct);
                output = result.StdOut;
            }

            return ParseDumpsysPackage(output, packageName);
        }

        private (string, string, long, bool) ParseDumpsysPackage(string output, string packageName)
        {
            var label = packageName; // fallback to package name
            var versionName = "Unknown";
            long versionCode = 0;
            var isSystem = false;

            if (string.IsNullOrWhiteSpace(output))
                return (label, versionName, versionCode, isSystem);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Application label
                if (trimmed.StartsWith("applicationLabel="))
                {
                    label = trimmed.Substring("applicationLabel=".Length).Trim();
                }
                // Version info
                else if (trimmed.StartsWith("versionName="))
                {
                    versionName = trimmed.Substring("versionName=".Length).Trim();
                }
                else if (trimmed.StartsWith("versionCode="))
                {
                    var versionStr = trimmed.Substring("versionCode=".Length).Trim();
                    var parts = versionStr.Split(' ');
                    if (parts.Length > 0 && long.TryParse(parts[0], out var code))
                    {
                        versionCode = code;
                    }
                }
                // System app check
                else if (trimmed.Contains("SYSTEM") || trimmed.Contains("system=true"))
                {
                    isSystem = true;
                }
            }

            return (label, versionName, versionCode, isSystem);
        }

        private async Task<long> CalculatePackageSizeOptimizedAsync(
            string serial,
            IReadOnlyList<string> paths,
            CancellationToken ct)
        {
            if (paths.Count == 0) return 0;

            try
            {
                // Use single command for all paths when possible
                if (paths.Count == 1)
                {
                    var cmd = $"-s {serial} shell stat -c %s \"{paths[0]}\"";
                    var result = await Proc.RunAsync(_adb.AdbPath, cmd, null, _logBus != null ? new Progress<string>(_logBus.Write) : null, ct);
                    var code = result.ExitCode;
                    var output = result.StdOut;
                    return long.TryParse(output.Trim(), out var size) ? size : 0;
                }

                // For multiple paths, use parallel execution with timeout
                var sizeTasks = paths.Select(async path =>
                {
                    try
                    {
                        var cmd = $"-s {serial} shell stat -c %s \"{path}\"";
                        var result = await Proc.RunAsync(_adb.AdbPath, cmd, null, _logBus != null ? new Progress<string>(_logBus.Write) : null, ct);
                        var code = result.ExitCode;
                        var output = result.StdOut;
                        return long.TryParse(output.Trim(), out var size) ? size : 0L;
                    }
                    catch
                    {
                        return 0L;
                    }
                });

                var sizes = await Task.WhenAll(sizeTasks);
                return sizes.Sum();
            }
            catch
            {
                return 0;
            }
        }

        private IReadOnlyList<InstalledApp> ApplyFilters(List<InstalledApp> apps, AppQueryOptions opts)
        {
            // Final deduplication using HashSet with custom comparer
            var uniqueApps = new HashSet<InstalledApp>(apps, new PackageNameEqualityComparer()).ToList();
            var filtered = uniqueApps.AsEnumerable();

            // Filter by app type
            if (opts.OnlyUserApps)
            {
                filtered = filtered.Where(app => !app.IsSystemApp);
            }
            else if (!opts.IncludeSystemApps)
            {
                filtered = filtered.Where(app => !app.IsSystemApp);
            }

            // Filter by keyword
            if (!string.IsNullOrWhiteSpace(opts.KeywordFilter))
            {
                var keyword = opts.KeywordFilter.Trim().ToLowerInvariant();
                filtered = filtered.Where(app =>
                    app.PackageName.ToLowerInvariant().Contains(keyword) ||
                    app.Label.ToLowerInvariant().Contains(keyword));
            }

            // Sort by display name
            return filtered.OrderBy(app => app.DisplayName).ToList();
        }

        // Custom equality comparer for final deduplication
        private sealed class PackageNameEqualityComparer : IEqualityComparer<InstalledApp>
        {
            public bool Equals(InstalledApp? x, InstalledApp? y)
            {
                if (x is null && y is null) return true;
                if (x is null || y is null) return false;
                return x.PackageName.Equals(y.PackageName, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(InstalledApp obj)
            {
                return obj.PackageName.GetHashCode(StringComparison.OrdinalIgnoreCase);
            }
        }

        private record PackageEntry(string PackageName, string BasePath);
    }
}