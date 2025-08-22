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
}
}