using AdbInstallerApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;// cspell:disable-line


namespace AdbInstallerApp.ViewModels
{
public class DeviceViewModel : ObservableObject
{
public DeviceInfo Model { get; }
public DeviceViewModel(DeviceInfo model) { Model = model; }


private bool _isSelected;
public bool IsSelected
{
get => _isSelected;
set => SetProperty(ref _isSelected, value);
}


public string Serial => Model.Serial;
public string State => Model.State;
public string DisplayName => !string.IsNullOrEmpty(Model.Manufacturer) && !string.IsNullOrEmpty(Model.Model) 
    ? $"{Model.Manufacturer} {Model.Model}" 
    : Model.Serial;
public string DeviceInfo => !string.IsNullOrEmpty(Model.AndroidVersion) 
    ? $"Android {Model.AndroidVersion} (API {Model.Sdk})" 
    : Model.State;
public string RootStatus => Model.IsRooted ? "🔓 Rooted" : "🔒 Unrooted";
public string Architecture => !string.IsNullOrEmpty(Model.Abi) ? Model.Abi.ToUpper() : "";
public string BuildInfo => !string.IsNullOrEmpty(Model.BuildNumber) ? Model.BuildNumber : "";
}
}