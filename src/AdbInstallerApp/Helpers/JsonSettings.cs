using Newtonsoft.Json; // cspell:disable-line
using System.IO;


namespace AdbInstallerApp.Helpers
{
public class AppSettings
{
public string ApkRepoPath { get; set; } = string.Empty;
public bool Reinstall { get; set; } = true; // -r
public bool GrantPerms { get; set; } = false; // -g
public bool Downgrade { get; set; } = false; // -d

// Enhanced installation settings
public bool EnableParallelInstall { get; set; } = false;
public int MaxConcurrentDevices { get; set; } = 3;
public bool AutoSelectOptimalSplits { get; set; } = true;
public bool ShowCompatibilityWarnings { get; set; } = true;

// Tool paths
public string AdbPath { get; set; } = "";
public string AaptPath { get; set; } = "";

// Logging settings
public bool EnableDetailedLogging { get; set; } = false;
public int LogRetentionDays { get; set; } = 7;
}


public static class JsonSettings
{
private static readonly string SettingsPath =
Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
"AdbInstallerApp", "settings.json");


public static AppSettings Load()
{
try
{
if (File.Exists(SettingsPath))
{
var json = File.ReadAllText(SettingsPath);
return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
}
}
catch { }
return new AppSettings();
}


public static void Save(AppSettings s)
{
try
{
var dir = Path.GetDirectoryName(SettingsPath)!;
if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(s, Formatting.Indented));
}
catch { }
}
}
}