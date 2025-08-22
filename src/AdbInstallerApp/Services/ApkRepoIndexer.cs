using AdbInstallerApp.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Linq;


namespace AdbInstallerApp.Services
{
public class ApkRepoIndexer : IDisposable
{
private FileSystemWatcher? _watcher;
private readonly ApkAnalyzer _analyzer;
public ObservableCollection<ApkItem> Items { get; } = new();
public ObservableCollection<ApkGroup> ApkGroups { get; } = new();
public string RepoPath { get; private set; } = string.Empty;

public ApkRepoIndexer()
{
    _analyzer = new ApkAnalyzer();
}


public void SetRepo(string? path)
{
RepoPath = path ?? string.Empty;
Refresh();
SetupWatcher();
}


private void SetupWatcher()
{
_watcher?.Dispose();
if (string.IsNullOrWhiteSpace(RepoPath) || !Directory.Exists(RepoPath)) return;
_watcher = new FileSystemWatcher(RepoPath, "*.apk")
{
IncludeSubdirectories = false,
EnableRaisingEvents = true
};
_watcher.Created += (_, __) => Refresh();
_watcher.Deleted += (_, __) => Refresh();
_watcher.Renamed += (_, __) => Refresh();
_watcher.Changed += (_, __) => Refresh();
}


        public void Refresh()
        {
            Application.Current?.Dispatcher.Invoke(() =>
{
Items.Clear();
if (!string.IsNullOrWhiteSpace(RepoPath) && Directory.Exists(RepoPath))
{
foreach (var f in Directory.GetFiles(RepoPath, "*.apk"))
{
var fileInfo = new FileInfo(f);
var apkItem = new ApkItem
{
FilePath = f,
FileName = Path.GetFileName(f),
FileSize = fileInfo.Length,
LastModified = fileInfo.LastWriteTime
};

                // Parse manifest and detect type
                try
                {
                    // Use Task.Run to avoid blocking UI thread
                    var manifestTask = Task.Run(async () => await _analyzer.ParseManifestAsync(f));
                    apkItem.Manifest = manifestTask.Result;
                    apkItem.Type = _analyzer.DetectApkType(Path.GetFileName(f), apkItem.Manifest);
                    
                    // Set SplitTag based on detected type
                    if (apkItem.Type == ApkType.SplitAbi)
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        if (fileName.Contains("arm64")) apkItem.SplitTag = "arm64-v8a";
                        else if (fileName.Contains("arm")) apkItem.SplitTag = "armeabi-v7a";
                        else if (fileName.Contains("x86_64")) apkItem.SplitTag = "x86_64";
                        else if (fileName.Contains("x86")) apkItem.SplitTag = "x86";
                    }
                    else if (apkItem.Type == ApkType.SplitDpi)
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        if (fileName.Contains("xxxhdpi")) apkItem.SplitTag = "480dpi";
                        else if (fileName.Contains("xxhdpi")) apkItem.SplitTag = "420dpi";
                        else if (fileName.Contains("xhdpi")) apkItem.SplitTag = "320dpi";
                        else if (fileName.Contains("hdpi")) apkItem.SplitTag = "240dpi";
                        else if (fileName.Contains("mdpi")) apkItem.SplitTag = "160dpi";
                        else if (fileName.Contains("ldpi")) apkItem.SplitTag = "120dpi";
                    }
                    else if (apkItem.Type == ApkType.SplitLanguage)
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        if (fileName.Contains("en")) apkItem.SplitTag = "en";
                        else if (fileName.Contains("vi")) apkItem.SplitTag = "vi";
                        else if (fileName.Contains("fr")) apkItem.SplitTag = "fr";
                        else if (fileName.Contains("de")) apkItem.SplitTag = "de";
                        else if (fileName.Contains("es")) apkItem.SplitTag = "es";
                        else if (fileName.Contains("ja")) apkItem.SplitTag = "ja";
                        else if (fileName.Contains("ko")) apkItem.SplitTag = "ko";
                        else if (fileName.Contains("zh")) apkItem.SplitTag = "zh";
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue
                    System.Diagnostics.Debug.WriteLine($"Failed to parse APK {f}: {ex.Message}");
                }

Items.Add(apkItem);
}

// Group APKs after parsing
GroupApksByPackage();
}
});
}

private void GroupApksByPackage()
{
    var groups = Items
        .Where(apk => !string.IsNullOrEmpty(apk.PackageName))
        .GroupBy(apk => apk.PackageName)
        .Where(g => !string.IsNullOrEmpty(g.Key))
        .Select(g => new ApkGroup
        {
            PackageName = g.Key!,
            BaseApk = g.FirstOrDefault(x => x.Type == ApkType.Base) ?? new ApkItem(),
            SplitApks = g.Where(x => x.Type != ApkType.Base).ToList(),
            DisplayName = GetDisplayName(g)
        });
        
            ApkGroups.Clear();
        foreach (var group in groups)
        {
            ApkGroups.Add(group);
        }
    }
    
    private static string GetDisplayName(IGrouping<string, ApkItem> group)
    {
        var firstItem = group.FirstOrDefault();
        if (firstItem?.AppLabel != null)
            return firstItem.AppLabel;
        return group.Key ?? "Unknown App";
    }


public void Dispose() => _watcher?.Dispose();
}
}