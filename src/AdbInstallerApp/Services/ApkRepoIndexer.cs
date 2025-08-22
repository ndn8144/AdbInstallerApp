using AdbInstallerApp.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;


namespace AdbInstallerApp.Services
{
public class ApkRepoIndexer : IDisposable
{
private FileSystemWatcher? _watcher;
public ObservableCollection<ApkItem> Items { get; } = new();
public string RepoPath { get; private set; } = string.Empty;


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
Items.Add(new ApkItem
{
FilePath = f,
FileName = Path.GetFileName(f),
FileSize = fileInfo.Length,
LastModified = fileInfo.LastWriteTime
});
}
}
});
}


public void Dispose() => _watcher?.Dispose();
}
}