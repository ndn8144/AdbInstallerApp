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
}
}