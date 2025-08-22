using AdbInstallerApp.Models;
using AdbInstallerApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace AdbInstallerApp.ViewModels
{
public partial class DeviceViewModel : ObservableObject
{
    private readonly UsbDebugHelper _usbDebugHelper;

    public DeviceViewModel(DeviceInfo model) 
    { 
        Model = model; 
        _usbDebugHelper = new UsbDebugHelper();
    }

    public DeviceInfo Model { get; }

    [ObservableProperty]
    private bool _isSelected;

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

// USB Debug Status
public UsbDebugInfo DebugInfo => _usbDebugHelper.AnalyzeDeviceState(Model);

public string DebugStatusIcon => DebugInfo.Status switch
{
    UsbDebugStatus.Ready => "🟢",
    UsbDebugStatus.NeedAuthorize => "🟡",
    UsbDebugStatus.Offline => "🔴",
    _ => "⚪"
};

        public string DebugStatusText => DebugInfo.Title;

        // Visual indicators for device status
        public bool NeedsAttention => DebugInfo.Status == UsbDebugStatus.NeedAuthorize || DebugInfo.Status == UsbDebugStatus.Offline;
        
        public string StatusColor => DebugInfo.Status switch
        {
            UsbDebugStatus.Ready => "#4CAF50",      // Green
            UsbDebugStatus.NeedAuthorize => "#FF9800", // Orange
            UsbDebugStatus.Offline => "#F44336",    // Red
            _ => "#9E9E9E"                          // Gray
        };

        // Commands
        public IRelayCommand ShowDebugHelpCommand => new RelayCommand(ShowDebugHelp);

private void ShowDebugHelp()
{
    var message = $"{DebugInfo.Title}\n\n{DebugInfo.Message}";
    
    if (DebugInfo.Steps.Count > 0)
    {
        message += "\n\nCác bước khắc phục:\n";
        foreach (var step in DebugInfo.Steps)
        {
            message += $"\n{step}";
        }
    }

    MessageBox.Show(message, "USB Debug Helper", MessageBoxButton.OK, 
        DebugInfo.Status == UsbDebugStatus.Ready ? MessageBoxImage.Information : MessageBoxImage.Warning);
}
}
}