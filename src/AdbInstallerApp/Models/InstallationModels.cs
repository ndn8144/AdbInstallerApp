using System;
using System.Collections.Generic;
using System.Linq;

namespace AdbInstallerApp.Models
{
    /// <summary>
    /// Represents a single APK file with metadata
    /// </summary>
    public record ApkFile(
        string Path,
        string PackageName,
        bool IsBase,
        string? Abi,
        string? Dpi,
        string? Locale,
        long VersionCode,
        string? SplitName = null,
        string? Sha256 = null,
        long SizeBytes = 0,
        string? SignerDigest = null,
        int? MinSdk = null,
        int? TargetSdk = null,
        IReadOnlyList<string>? RequiredSplits = null,
        IReadOnlyList<string>? RequiredFeatures = null)
    {
        public string FileName => System.IO.Path.GetFileName(Path);
        public bool IsValid => !string.IsNullOrEmpty(Path) && !string.IsNullOrEmpty(PackageName);
        public string DisplayName => IsBase ? $"{PackageName} (base)" : $"{PackageName} ({SplitName ?? "split"})";
        
        /// <summary>
        /// Check if this APK is compatible with device SDK level
        /// </summary>
        public bool IsCompatibleWithSdk(int deviceSdk) => MinSdk == null || deviceSdk >= MinSdk;
        
        /// <summary>
        /// Get profile identifier for conflict detection (same profile = potential conflict)
        /// </summary>
        public string GetProfileId() => $"{Abi ?? "any"}:{Dpi ?? "any"}:{Locale ?? "any"}";
    }

    /// <summary>
    /// Represents an installation unit (single APK or APK group)
    /// </summary>
    public record InstallationUnit(string PackageName, IReadOnlyList<ApkFile> Files, long TotalBytes = 0)
    {
        public bool IsGroup => Files.Count > 1;
        public bool IsValid => Files.Count > 0 && Files.All(f => f.IsValid);
        public ApkFile? BaseApk => Files.FirstOrDefault(f => f.IsBase);
        public IEnumerable<ApkFile> SplitApks => Files.Where(f => !f.IsBase);
        public long TotalSize => TotalBytes > 0 ? TotalBytes : Files.Sum(f => GetFileSize(f.Path));
        
        /// <summary>
        /// Get formatted size for display
        /// </summary>
        public string FormattedSize => FormatBytes(TotalSize);
        
        private static long GetFileSize(string path)
        {
            try
            {
                return new System.IO.FileInfo(path).Length;
            }
            catch
            {
                return 0;
            }
        }
        
        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }


    /// <summary>
    /// Device properties for split APK matching
    /// </summary>
    public record DeviceProps(
        string Serial,
        string Model, 
        string Abilist, 
        int Density, 
        int Sdk,
        string State = "device",
        string? Locale = null)
    {
        public bool IsOnline => State == "device";
        public bool IsUnauthorized => State == "unauthorized";
        public bool IsOffline => State == "offline";
        
        public IReadOnlyList<string> SupportedAbis => 
            Abilist.Split(',', StringSplitOptions.RemoveEmptyEntries)
                   .Select(s => s.Trim())
                   .ToList();
        
        public string DensityBucket => Density switch
        {
            <= 140 => "ldpi",
            <= 200 => "mdpi", 
            <= 280 => "hdpi",
            <= 400 => "xhdpi",
            <= 560 => "xxhdpi",
            _ => "xxxhdpi"
        };
        
        /// <summary>
        /// Check if device supports the given ABI
        /// </summary>
        public bool SupportsAbi(string abi)
        {
            if (string.IsNullOrEmpty(abi)) return true; // Universal APK
            return SupportedAbis.Contains(abi, StringComparer.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Check if device density matches the given DPI bucket
        /// </summary>
        public bool MatchesDensity(string? dpi)
        {
            if (string.IsNullOrEmpty(dpi)) return true; // Universal density
            return string.Equals(DensityBucket, dpi, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Installation plan for a single device
    /// </summary>
    public record DeviceInstallPlan(
        string Serial, 
        IReadOnlyList<InstallationUnit> Units, 
        Models.InstallOptions Options,
        DeviceProps? DeviceProperties = null)
    {
        public bool IsValid => Units.Count > 0 && Units.All(u => u.IsValid);
        public int TotalFiles => Units.Sum(u => u.Files.Count);
        public long TotalSize => Units.Sum(u => u.TotalSize);
        public bool CanExecute => DeviceProperties?.IsOnline == true;
        
        public string GetSummary()
        {
            var packages = Units.Count;
            var files = TotalFiles;
            var size = FormatBytes(TotalSize);
            return $"{packages} package(s), {files} file(s), {size}";
        }
        
        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }

    /// <summary>
    /// Summary of installation results
    /// </summary>
    public record InstallationSummary(
        int TotalDevices,
        int TotalUnits,
        int SuccessfulUnits,
        int FailedUnits,
        int SkippedUnits,
        TimeSpan Duration,
        List<string> Errors)
    {
        public bool IsSuccess => FailedUnits == 0;
        public double SuccessRate => TotalUnits > 0 ? (double)SuccessfulUnits / TotalUnits : 0;
        
        public string GetSummary()
        {
            return $"Completed: {SuccessfulUnits}/{TotalUnits} units on {TotalDevices} device(s) in {Duration:mm\\:ss}";
        }
    }

    /// <summary>
    /// Selected items for installation (input)
    /// </summary>
    public abstract record SelectedItem(string DisplayName);
    
    public record SelectedApkFile(string Path, string DisplayName) : SelectedItem(DisplayName);
    
    public record SelectedApkGroup(string PackageName, IReadOnlyList<string> FilePaths, string DisplayName) 
        : SelectedItem(DisplayName);

    /// <summary>
    /// Device state for UI display
    /// </summary>
    public enum DeviceInstallState
    {
        Pending,
        InProgress,
        Completed,
        Failed,
        Skipped
    }

    /// <summary>
    /// Unit state for UI display
    /// </summary>
    public enum UnitInstallState
    {
        Pending,
        InProgress,
        Completed,
        Failed,
        Skipped
    }

    /// <summary>
    /// Per-device installation options with advanced configuration
    /// </summary>
    public record DeviceInstallOptions(
        bool Reinstall = false,
        bool AllowDowngrade = false,
        bool GrantRuntimePermissions = false,
        int? UserId = null,
        bool PreferHighestCompatible = true,
        StrictSplitMatchMode StrictSplitMatch = StrictSplitMatchMode.Strict,
        InstallStrategy InstallStrategy = InstallStrategy.Auto,
        bool VerifySignature = true,
        bool VerifyVersionHomogeneity = true,
        int? ThrottleMBps = null,
        int MaxRetries = 2,
        TimeSpan Timeout = default)
    {
        public TimeSpan EffectiveTimeout => Timeout == default ? TimeSpan.FromMinutes(5) : Timeout;
    }

    /// <summary>
    /// Global installation options (fallback for per-device)
    /// </summary>
    public record InstallOptionsGlobal(
        bool Reinstall = false,
        bool AllowDowngrade = false,
        bool GrantRuntimePermissions = false,
        int? UserId = null,
        int MaxRetries = 2,
        TimeSpan Timeout = default);

    /// <summary>
    /// Split matching behavior for APK groups
    /// </summary>
    public enum StrictSplitMatchMode
    {
        /// <summary>Require all mandatory splits (ABI/DPI/requiredSplit/feature)</summary>
        Strict,
        /// <summary>Skip non-matching splits (ABI/DPI/locale)</summary>
        Relaxed,
        /// <summary>Fall back to base-only installation if splits don't match</summary>
        BaseOnlyFallback
    }

    /// <summary>
    /// Installation strategy selection
    /// </summary>
    public enum InstallStrategy
    {
        /// <summary>Auto-select based on file count and path complexity</summary>
        Auto,
        /// <summary>Use adb install-multiple command</summary>
        InstallMultiple,
        /// <summary>Use pm install-create/write/commit session</summary>
        PmSession
    }

    /// <summary>
    /// Device installation plan with per-device options
    /// </summary>
    public record DevicePlan(
        string Serial,
        IReadOnlyList<InstallationUnit> Units,
        DeviceInstallOptions Options)
    {
        public bool IsValid => Units.Count > 0 && Units.All(u => u.IsValid);
        public int TotalFiles => Units.Sum(u => u.Files.Count);
        public long TotalBytes => Units.Sum(u => u.TotalBytes);
        public string FormattedSize => FormatBytes(TotalBytes);
        
        public string GetSummary()
        {
            var packages = Units.Count;
            var files = TotalFiles;
            return $"{packages} package(s), {files} file(s), {FormattedSize}";
        }
        
        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }

    /// <summary>
    /// APK group snapshot for validation and conflict detection
    /// </summary>
    public record GroupSnapshot(
        string PackageName,
        IReadOnlyList<ApkFile> Files,
        DateTime SnapshotTime)
    {
        public IReadOnlyList<ApkFile> Bases => Files.Where(f => f.IsBase).ToList();
        public IReadOnlyList<ApkFile> Splits => Files.Where(f => !f.IsBase).ToList();
        
        /// <summary>
        /// Validate group integrity and detect conflicts
        /// </summary>
        public ValidationResult Validate(DeviceInstallOptions options)
        {
            var errors = new List<string>();
            
            // Rule 1: Exactly one base
            if (Bases.Count != 1)
            {
                errors.Add($"Group must contain exactly one base APK (found {Bases.Count})");
            }
            
            // Rule 2: Consistent package name
            var distinctPackages = Files.Select(f => f.PackageName).Distinct().Count();
            if (distinctPackages > 1)
            {
                errors.Add("Mixed package names in group");
            }
            
            // Rule 3: Version homogeneity (if enabled)
            if (options.VerifyVersionHomogeneity && Bases.Count == 1)
            {
                var baseVersion = Bases[0].VersionCode;
                var versionMismatches = Splits.Where(s => s.VersionCode != baseVersion).ToList();
                if (versionMismatches.Any())
                {
                    errors.Add($"Version mismatch: {versionMismatches.Count} splits have different versionCode than base");
                }
            }
            
            // Rule 4: Signature consistency (if enabled)
            if (options.VerifySignature && Bases.Count == 1)
            {
                var baseSignature = Bases[0].SignerDigest;
                if (!string.IsNullOrEmpty(baseSignature))
                {
                    var signatureMismatches = Files.Where(f => f.SignerDigest != baseSignature).ToList();
                    if (signatureMismatches.Any())
                    {
                        errors.Add($"Signature mismatch: {signatureMismatches.Count} files have different signature than base");
                    }
                }
            }
            
            // Rule 5: Duplicate split conflicts
            var duplicateGroups = Splits.GroupBy(s => s.GetProfileId())
                                      .Where(g => g.Select(x => x.Sha256).Distinct().Count() > 1)
                                      .ToList();
            
            foreach (var dupGroup in duplicateGroups)
            {
                errors.Add($"Conflicting duplicates for profile {dupGroup.Key}: different content but same ABI/DPI/locale");
            }
            
            return new ValidationResult(errors.Count == 0, errors);
        }
    }

    /// <summary>
    /// Validation result with detailed error information
    /// </summary>
    public record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
    {
        public string ErrorSummary => string.Join("; ", Errors);
    }

    /// <summary>
    /// Progress handle for tracking installation progress
    /// </summary>
    public interface IProgressHandle : IDisposable
    {
        CancellationToken Token { get; }
        void SetStatus(string status);
        void ReportAbsolute(long completedBytes);
        void Report(long deltaBytes);
        void Complete(string? finalStatus = null);
        void Fail(string error);
        void Cancel();
        IProgressHandle CreateSlice(string name, double weight, long totalBytes = 0);
        void Indeterminate(string status);
    }

    /// <summary>
    /// Installation phase enumeration
    /// </summary>
    public enum InstallationPhase
    {
        Validate = 0,    // 15% weight
        Session = 1,     // 10% weight  
        Write = 2,       // 60% weight
        Commit = 3       // 15% weight
    }
}
