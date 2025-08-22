namespace AdbInstallerApp.Models
{
public class ApkItem
{
public string FilePath { get; set; } = string.Empty;
public string FileName { get; set; } = string.Empty;
public string Package { get; set; } = string.Empty; // optional, if parsed
public string SplitTag { get; set; } = string.Empty; // e.g., arm64, xxhdpi
}
}