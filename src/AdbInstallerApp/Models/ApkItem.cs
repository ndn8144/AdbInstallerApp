namespace AdbInstallerApp.Models
{
public class ApkItem
{
public string FilePath { get; set; } = string.Empty;
public string FileName { get; set; } = string.Empty;
public string Package { get; set; } = string.Empty; // optional, if parsed
public string SplitTag { get; set; } = string.Empty; // e.g., arm64, xxhdpi
public long FileSize { get; set; } = 0; // File size in bytes
public DateTime LastModified { get; set; } = DateTime.MinValue; // Last modified date
public string Version { get; set; } = string.Empty; // APK version if available
public string TargetSdk { get; set; } = string.Empty; // Target SDK version
public string MinSdk { get; set; } = string.Empty; // Minimum SDK version

// Enhanced properties from manifest
public ApkManifest Manifest { get; set; } = new ApkManifest();
public ApkType Type { get; set; } = ApkType.Base;

// Convenience properties
public string PackageName => Manifest?.PackageName ?? Package;
public string VersionName => Manifest?.VersionName ?? Version;
public string VersionCode => Manifest?.VersionCode ?? "";
public string AppLabel => Manifest?.AppLabel ?? FileName;
public List<string> SupportedAbis => Manifest?.SupportedAbis ?? new List<string>();
public string TargetSdkVersion => Manifest?.TargetSdk ?? TargetSdk;
public bool IsSplit => Manifest?.IsSplit ?? false;

// Display properties
public string DisplayInfo => !string.IsNullOrEmpty(AppLabel) 
    ? $"{AppLabel} v{VersionName}" 
    : FileName;
}
}