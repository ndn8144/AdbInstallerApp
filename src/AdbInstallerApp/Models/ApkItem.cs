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
}
}