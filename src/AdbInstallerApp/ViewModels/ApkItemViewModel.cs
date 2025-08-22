using AdbInstallerApp.Models;
using CommunityToolkit.Mvvm.ComponentModel; // cspell:disable-line


namespace AdbInstallerApp.ViewModels
{
public class ApkItemViewModel : ObservableObject
{
public ApkItem Model { get; }
public ApkItemViewModel(ApkItem m) { Model = m; }


private bool _isSelected;
public bool IsSelected
{
get => _isSelected;
set => SetProperty(ref _isSelected, value);
}


public string FileName => Model.FileName;
public string FilePath => Model.FilePath;
public string FileSize => Model.FileSize > 0 ? FormatFileSize(Model.FileSize) : "";
public string LastModified => Model.LastModified != DateTime.MinValue ? Model.LastModified.ToString("yyyy-MM-dd HH:mm") : "";
public string DisplayInfo => !string.IsNullOrEmpty(Model.AppLabel) ? Model.AppLabel : Model.FileName;
public string PackageName => Model.PackageName;
public string VersionInfo => !string.IsNullOrEmpty(Model.VersionName) ? $"v{Model.VersionName}" : "";
public string TargetSdkVersion => !string.IsNullOrEmpty(Model.TargetSdkVersion) ? $"API {Model.TargetSdkVersion}" : "";
public string Type => Model.Type.ToString();
public string SplitTag => Model.SplitTag;

private static string FormatFileSize(long bytes)
{
string[] sizes = { "B", "KB", "MB", "GB" };
double len = bytes;
int order = 0;
while (len >= 1024 && order < sizes.Length - 1)
{
order++;
len = len / 1024;
}
return $"{len:0.##} {sizes[order]}";
}
}
}