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
        long VersionCode)
    {
        public string FileName => System.IO.Path.GetFileName(Path);
        public bool IsValid => !string.IsNullOrEmpty(Path) && !string.IsNullOrEmpty(PackageName);
    }

    /// <summary>
    /// Represents an installation unit (single APK or APK group)
    /// </summary>
    public record InstallationUnit(string PackageName, IReadOnlyList<ApkFile> Files)
    {
        public bool IsGroup => Files.Count > 1;
        public bool IsValid => Files.Count > 0 && Files.All(f => f.IsValid);
        public ApkFile? BaseApk => Files.FirstOrDefault(f => f.IsBase);
        public IEnumerable<ApkFile> SplitApks => Files.Where(f => !f.IsBase);
        public long TotalSize => Files.Sum(f => GetFileSize(f.Path));
        
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
        string State = "device")
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
}
