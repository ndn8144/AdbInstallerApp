using AdbInstallerApp.Models;
using AdbInstallerApp.Helpers;
using System.Text.RegularExpressions;

namespace AdbInstallerApp.Services
{
    public sealed class AppInventoryService
    {
        private readonly AdbService _adb;

        public AppInventoryService(AdbService adb)
        {
            _adb = adb ?? throw new ArgumentNullException(nameof(adb));
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
                // 1) List packages (try modern cmd first, fallback to pm)
                var packages = await ListPackagesAsync(serial, opts.UserId, ct);

                if (packages.Count == 0)
                    return Array.Empty<InstalledApp>();

                // 2) Enrich with detailed info
                var results = new List<InstalledApp>();
                var semaphore = new SemaphoreSlim(3); // Limit concurrent requests

                var tasks = packages.Select(async pkg =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        return await EnrichPackageInfoAsync(serial, pkg, opts.WithSizes, ct);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                var enrichedApps = await Task.WhenAll(tasks);
                results.AddRange(enrichedApps.Where(app => app != null)!);

                // 3) Apply filters
                return ApplyFilters(results, opts);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to list installed apps for {serial}: {ex.Message}", ex);
            }
        }

        private async Task<List<PackageEntry>> ListPackagesAsync(string serial, int userId, CancellationToken ct)
        {
            // Try modern cmd package first  
            var cmd = $"-s {serial} shell cmd package list packages -f --user {userId}";
            var (code, output, _) = await ProcessRunner.RunAsync(_adb.AdbPath, cmd, timeoutMs: 15000);

            if (code != 0 || string.IsNullOrWhiteSpace(output))
            {
                // Fallback to pm
                cmd = $"-s {serial} shell pm list packages -f --user {userId}";
                (_, output, _) = await ProcessRunner.RunAsync(_adb.AdbPath, cmd, timeoutMs: 15000);
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

        private async Task<InstalledApp?> EnrichPackageInfoAsync(
            string serial,
            PackageEntry pkg,
            bool withSizes,
            CancellationToken ct)
        {
            try
            {
                // Get package paths (base + splits)
                var paths = await GetPackagePathsAsync(serial, pkg.PackageName, ct);

                // Get package info from dumpsys
                var (label, versionName, versionCode, isSystem) =
                    await GetPackageDetailsAsync(serial, pkg.PackageName, ct);

                // Get size if requested
                long? totalSize = null;
                if (withSizes)
                {
                    totalSize = await CalculatePackageSizeAsync(serial, paths, ct);
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
            var (_, output, _) = await ProcessRunner.RunAsync(_adb.AdbPath, cmd, timeoutMs: 8000);

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
            GetPackageDetailsAsync(string serial, string packageName, CancellationToken ct)
        {
            var cmd = $"-s {serial} shell dumpsys package {packageName}";
            var (_, output, _) = await ProcessRunner.RunAsync(_adb.AdbPath, cmd, timeoutMs: 10000);

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

        private async Task<long> CalculatePackageSizeAsync(
            string serial,
            IReadOnlyList<string> paths,
            CancellationToken ct)
        {
            long totalSize = 0;

            foreach (var path in paths)
            {
                try
                {
                    var cmd = $"-s {serial} shell stat -c %s \"{path}\"";
                    var (_, output, _) = await ProcessRunner.RunAsync(_adb.AdbPath, cmd, timeoutMs: 5000);

                    if (long.TryParse(output.Trim(), out var size))
                    {
                        totalSize += size;
                    }
                }
                catch
                {
                    // Skip if can't get size for this path
                }
            }

            return totalSize;
        }

        private IReadOnlyList<InstalledApp> ApplyFilters(List<InstalledApp> apps, AppQueryOptions opts)
        {
            var filtered = apps.AsEnumerable();

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

        private record PackageEntry(string PackageName, string BasePath);
    }
}