using AdbInstallerApp.Models;
using System.Security.Cryptography;

namespace AdbInstallerApp.Services;

public interface IApkValidator
{
    Task<ValidationResult> ValidateApkFileAsync(ApkFile apk, CancellationToken ct = default);
    ValidationResult ValidateGroup(GroupSnapshot group, DeviceInstallOptions options);
    ValidationResult ValidateDeviceCompatibility(GroupSnapshot group, DeviceProps device, DeviceInstallOptions options);
}

public sealed class ApkValidator : IApkValidator
{
    private readonly ILogBus _log;
    
    public ApkValidator(ILogBus log)
    {
        _log = log;
    }

    public async Task<ValidationResult> ValidateApkFileAsync(ApkFile apk, CancellationToken ct = default)
    {
        var errors = new List<string>();
        
        try
        {
            // 1. File existence and accessibility
            if (!File.Exists(apk.Path))
            {
                return new ValidationResult(false, new List<string> { $"APK file not found: {apk.Path}" });
            }

            var fileInfo = new FileInfo(apk.Path);
            
            // 2. File size validation
            if (fileInfo.Length == 0)
            {
                return new ValidationResult(false, new List<string> { "APK file is empty" });
            }
            
            if (apk.SizeBytes > 0 && fileInfo.Length != apk.SizeBytes)
            {
                errors.Add($"File size mismatch: expected {apk.SizeBytes}, actual {fileInfo.Length}");
            }

            // 3. SHA256 integrity check (if available)
            if (!string.IsNullOrEmpty(apk.Sha256))
            {
                using var fileStream = File.OpenRead(apk.Path);
                var computedHash = await SHA256.HashDataAsync(fileStream, ct);
                var computedHashString = Convert.ToHexString(computedHash).ToLowerInvariant();
                
                if (!string.Equals(computedHashString, apk.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return new ValidationResult(false, new List<string> { "APK file corruption detected (SHA256 mismatch)" });
                }
            }

            // 4. APK format validation (basic ZIP structure check)
            if (!await IsValidApkStructureAsync(apk.Path, ct))
            {
                return new ValidationResult(false, new List<string> { "Invalid APK file structure" });
            }

            _log.WriteDebug($"APK validation passed: {Path.GetFileName(apk.Path)}");
            return new ValidationResult(errors.Count == 0, errors);
        }
        catch (UnauthorizedAccessException)
        {
            return new ValidationResult(false, new List<string> { "Cannot access APK file (permission denied)" });
        }
        catch (Exception ex)
        {
            _log.WriteError($"APK validation error: {ex.Message}");
            return new ValidationResult(false, new List<string> { $"Validation error: {ex.Message}" });
        }
    }

    public ValidationResult ValidateGroup(GroupSnapshot group, DeviceInstallOptions options)
    {
        var errors = new List<string>();

        // 1. Must have exactly one base APK
        if (group.Bases.Count == 0)
        {
            return new ValidationResult(false, new List<string> { $"Package {group.PackageName} has no base APK" });
        }
        
        if (group.Bases.Count > 1)
        {
            return new ValidationResult(false, 
                new List<string> { $"Package {group.PackageName} has {group.Bases.Count} base APKs (expected 1)" });
        }

        var baseApk = group.Bases[0];

        // 2. Package name consistency
        var inconsistentPackages = group.Files
            .Where(f => !string.Equals(f.PackageName, group.PackageName, StringComparison.OrdinalIgnoreCase))
            .ToList();
            
        if (inconsistentPackages.Any())
        {
            return new ValidationResult(false, 
                new List<string> { $"Inconsistent package names in group: {string.Join(", ", inconsistentPackages.Select(f => f.PackageName).Distinct())}" });
        }

        // 3. Version code homogeneity (if required)
        if (options.VerifyVersionHomogeneity)
        {
            var differentVersions = group.Files
                .Where(f => f.VersionCode != baseApk.VersionCode)
                .ToList();
                
            if (differentVersions.Any())
            {
                return new ValidationResult(false,
                    new List<string> { $"Version code mismatch: base={baseApk.VersionCode}, others={string.Join(",", differentVersions.Select(f => f.VersionCode).Distinct())}" });
            }
        }

        // 4. Signature consistency (if required)
        if (options.VerifySignature)
        {
            var baseSignature = baseApk.SignerDigest;
            if (!string.IsNullOrEmpty(baseSignature))
            {
                var mismatchedSignatures = group.Files
                    .Where(f => !string.IsNullOrEmpty(f.SignerDigest) && !string.Equals(f.SignerDigest, baseSignature, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                    
                if (mismatchedSignatures.Any())
                {
                    return new ValidationResult(false, 
                        new List<string> { "Signature mismatch between APK files in group" });
                }
            }
        }

        // 5. Split naming validation
        foreach (var split in group.Files.Where(f => !f.IsBase))
        {
            if (string.IsNullOrEmpty(split.SplitName))
            {
                errors.Add($"Split APK has no split name: {Path.GetFileName(split.Path)}");
            }
        }

        _log.WriteDebug($"Group validation passed: {group.PackageName} ({group.Files.Count} files)");
        return new ValidationResult(errors.Count == 0, errors);
    }

    public ValidationResult ValidateDeviceCompatibility(GroupSnapshot group, DeviceProps device, DeviceInstallOptions options)
    {
        var errors = new List<string>();
        var baseApk = group.Bases.FirstOrDefault();

        if (baseApk == null)
        {
            return new ValidationResult(false, new List<string> { "No base APK found in group" });
        }

        // 1. SDK version compatibility
        if (baseApk.MinSdk.HasValue && baseApk.MinSdk.Value > device.Sdk)
        {
            return new ValidationResult(false,
                new List<string> { $"App requires SDK {baseApk.MinSdk.Value}, device has SDK {device.Sdk}" });
        }

        if (baseApk.TargetSdk.HasValue && baseApk.TargetSdk.Value > device.Sdk + 5) // Allow some leeway
        {
            errors.Add($"App targets SDK {baseApk.TargetSdk.Value}, device has SDK {device.Sdk} - may have compatibility issues");
        }

        // 2. ABI compatibility
        var deviceAbis = device.SupportedAbis;
        var incompatibleSplits = group.Files
            .Where(f => !f.IsBase && !string.IsNullOrEmpty(f.Abi) && !deviceAbis.Contains(f.Abi))
            .ToList();

        if (options.StrictSplitMatch == StrictSplitMatchMode.Strict && incompatibleSplits.Any())
        {
            var incompatibleAbis = string.Join(", ", incompatibleSplits.Select(s => s.Abi).Distinct());
            return new ValidationResult(false,
                new List<string> { $"Incompatible ABI splits: {incompatibleAbis}. Device supports: {string.Join(", ", deviceAbis)}" });
        }

        // 3. Storage space check (approximate) - skip for now as DeviceProps doesn't have storage info
        var totalBytes = group.Files.Sum(f => f.SizeBytes);
        // TODO: Add storage check when available

        // 4. Density compatibility (warnings only)
        var deviceDensity = device.Density;
        var densitySpecificSplits = group.Files.Where(s => !s.IsBase && !string.IsNullOrEmpty(s.Dpi)).ToList();
        
        if (densitySpecificSplits.Any() && deviceDensity > 0)
        {
            var bestMatch = FindBestDensityMatch(densitySpecificSplits, deviceDensity);
            if (bestMatch == null)
            {
                errors.Add($"No density-specific splits match device density {deviceDensity}dpi");
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    private async Task<bool> IsValidApkStructureAsync(string apkPath, CancellationToken ct)
    {
        try
        {
            using var fileStream = File.OpenRead(apkPath);
            
            // Check ZIP signature (first 4 bytes should be PK\x03\x04)
            var buffer = new byte[4];
            await fileStream.ReadExactlyAsync(buffer, ct);
            
            return buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04;
        }
        catch
        {
            return false;
        }
    }

    private static ApkFile? FindBestDensityMatch(IEnumerable<ApkFile> densitySpecificSplits, int deviceDensity)
    {
        // Simple density matching - in real implementation, use proper density bucket matching
        return densitySpecificSplits
            .OrderBy(s => Math.Abs(ParseDensity(s.Dpi) - deviceDensity))
            .FirstOrDefault();
    }

    private static int ParseDensity(string? dpi)
    {
        if (string.IsNullOrEmpty(dpi)) return 0;
        
        // Extract numeric part from density strings like "hdpi", "480dpi", etc.
        var match = System.Text.RegularExpressions.Regex.Match(dpi, @"(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 
               dpi.ToLowerInvariant() switch
               {
                   "ldpi" => 120,
                   "mdpi" => 160,
                   "hdpi" => 240,
                   "xhdpi" => 320,
                   "xxhdpi" => 480,
                   "xxxhdpi" => 640,
                   _ => 160
               };
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


