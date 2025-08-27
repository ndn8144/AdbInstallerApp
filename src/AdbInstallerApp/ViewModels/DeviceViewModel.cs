using AdbInstallerApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;// cspell:disable-line
using System.Diagnostics;

namespace AdbInstallerApp.ViewModels
{
    public class DeviceViewModel : ObservableObject
    {
        public DeviceInfo Model { get; }
        
        public DeviceViewModel(DeviceInfo model) 
        { 
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // Basic Device Information
        public string Serial => Model?.Serial ?? "Unknown";
        public string State => Model?.State ?? "Unknown";
        public string ConnectionStatusIcon => Model?.ConnectionStatusIcon ?? "⚪";
        
        public string DisplayName 
        { 
            get 
            {
                if (Model == null) return "Unknown Device";
                
                var manufacturer = Model.Manufacturer?.Trim();
                var model = Model.Model?.Trim();
                var serial = Model.Serial?.Trim();
                
                // Simple and direct logic to avoid binding issues
                if (!string.IsNullOrEmpty(manufacturer) && !string.IsNullOrEmpty(model))
                {
                    var baseName = $"{manufacturer} {model}";
                    
                    if (!string.IsNullOrEmpty(serial))
                    {
                        return $"{baseName} ({serial})";
                    }
                    return baseName;
                }
                
                if (!string.IsNullOrEmpty(model))
                {
                    return !string.IsNullOrEmpty(serial) ? $"{model} ({serial})" : model;
                }
                
                if (!string.IsNullOrEmpty(manufacturer))
                {
                    return !string.IsNullOrEmpty(serial) ? $"{manufacturer} ({serial})" : manufacturer;
                }
                
                if (!string.IsNullOrEmpty(serial))
                {
                    return serial;
                }
                
                return "Unknown Device";
            }
        }
        
        public string DeviceInfo 
        { 
            get 
            {
                if (Model == null) return "Unknown";
                
                if (!string.IsNullOrEmpty(Model.AndroidVersion) && !string.IsNullOrEmpty(Model.Sdk))
                {
                    return $"Android {Model.AndroidVersion} (API {Model.Sdk})";
                }
                
                if (!string.IsNullOrEmpty(Model.AndroidVersion))
                {
                    return $"Android {Model.AndroidVersion}";
                }
                
                if (!string.IsNullOrEmpty(Model.State))
                {
                    return Model.State;
                }
                
                return "Unknown";
            }
        }
        
        // Security & Root Status
        public string RootStatus => Model?.IsRooted == true ? "🔓 Rooted" : "🔒 Unrooted";
        public string RootStatusIcon => Model?.RootStatusIcon ?? "🔒";
        public string SecurityStatus => Model?.IsSecure == true ? "🛡️ Secure" : "⚠️ Insecure";
        public string SecurityStatusIcon => Model?.SecurityStatusIcon ?? "⚠️";
        
        // Hardware Information
        public string Architecture => !string.IsNullOrEmpty(Model?.Abi) ? Model.Abi.ToUpper() : "Unknown";
        public string BuildInfo => !string.IsNullOrEmpty(Model?.BuildNumber) ? Model.BuildNumber : "Unknown";
        public string BuildType => !string.IsNullOrEmpty(Model?.BuildType) ? Model.BuildType.ToUpper() : "Unknown";
        
        // Screen & Display
        public string ScreenInfo 
        { 
            get 
            {
                if (Model == null) return "Unknown";
                
                var resolution = Model.ScreenResolution?.Trim();
                var density = Model.ScreenDensity?.Trim();
                
                if (!string.IsNullOrEmpty(resolution) && !string.IsNullOrEmpty(density))
                {
                    return $"{resolution} @ {density}dpi";
                }
                
                if (!string.IsNullOrEmpty(resolution))
                {
                    return resolution;
                }
                
                if (!string.IsNullOrEmpty(density))
                {
                    return $"{density}dpi";
                }
                
                return "Unknown";
            }
        }
        
        // Battery Information
        public string BatteryInfo 
        { 
            get 
            {
                if (Model == null) return "Unknown";
                
                var level = Model.BatteryLevel?.Trim();
                var status = Model.BatteryStatus?.Trim();
                var temp = Model.BatteryTemperature?.Trim();
                
                var parts = new List<string>();
                
                if (!string.IsNullOrEmpty(level))
                {
                    parts.Add($"{level}%");
                }
                
                if (!string.IsNullOrEmpty(status))
                {
                    parts.Add(status);
                }
                
                if (!string.IsNullOrEmpty(temp))
                {
                    parts.Add($"{temp}°C");
                }
                
                if (parts.Count > 0)
                {
                    return string.Join(" | ", parts);
                }
                
                return "Unknown";
            }
        }
        
        public string BatteryStatusIcon => Model?.BatteryStatusIcon ?? "🔋";
        
        // Network Information
        public string NetworkInfo 
        { 
            get 
            {
                if (Model == null) return "Unknown";
                
                var wifi = Model.WifiSsid?.Trim();
                var mobile = Model.HasMobileData;
                
                if (!string.IsNullOrEmpty(wifi))
                {
                    return $"WiFi: {wifi}";
                }
                
                if (mobile)
                {
                    return "Mobile Data";
                }
                
                return "No Network";
            }
        }
        
        // Developer Options
        public string DeveloperStatus 
        { 
            get 
            {
                if (Model == null) return "Unknown";
                
                var devOpts = Model.DeveloperOptionsEnabled;
                var adbEnabled = Model.AdbEnabled;
                var usbDebug = Model.UsbDebuggingEnabled;
                
                if (devOpts && adbEnabled && usbDebug)
                {
                    return "🔧 Full Access";
                }
                
                if (adbEnabled && usbDebug)
                {
                    return "🔧 ADB Ready";
                }
                
                if (devOpts)
                {
                    return "🔧 Dev Options";
                }
                
                return "❌ Restricted";
            }
        }
        
        // Troubleshooting Status
        public string ConnectionDiagnosis => Model?.ConnectionDiagnosis ?? "Unknown";
        
        public string ConnectionHealth 
        { 
            get 
            {
                if (Model == null) return "Unknown";
                
                var usbOk = Model.UsbConnectionOk;
                var driverOk = Model.DriverInstalled;
                var adbAuth = Model.AdbAuthorized;
                var compatible = Model.DeviceCompatible;
                
                if (usbOk && driverOk && adbAuth && compatible)
                {
                    return "🟢 Healthy";
                }
                
                if (usbOk && driverOk && adbAuth)
                {
                    return "🟡 Good";
                }
                
                if (usbOk && driverOk)
                {
                    return "🟡 Fair";
                }
                
                return "🔴 Issues";
            }
        }
        
        // Hardware Details
        public string HardwareSummary 
        { 
            get 
            {
                if (Model == null) return "Unknown";
                
                var abi = Model.Abi?.Trim();
                var density = Model.Density?.Trim();
                var memory = Model.MemoryInfo?.Trim();
                
                var parts = new List<string>();
                
                if (!string.IsNullOrEmpty(abi))
                {
                    parts.Add(abi.ToUpper());
                }
                
                if (!string.IsNullOrEmpty(density))
                {
                    parts.Add($"{density}dpi");
                }
                
                if (!string.IsNullOrEmpty(memory))
                {
                    // Extract memory size from memory info
                    if (memory.Contains("MemTotal:"))
                    {
                        var memLine = memory.Split('\n').FirstOrDefault(l => l.Contains("MemTotal:"));
                        if (memLine != null)
                        {
                            var memValue = memLine.Split(':')[1].Trim().Split(' ')[0];
                            if (long.TryParse(memValue, out var memKB))
                            {
                                var memGB = memKB / 1024.0 / 1024.0;
                                parts.Add($"{memGB:F1}GB RAM");
                            }
                        }
                    }
                }
                
                if (parts.Count > 0)
                {
                    return string.Join(" | ", parts);
                }
                
                return "Unknown";
            }
        }
        
        // Package Information
        public string PackageSummary 
        { 
            get 
            {
                if (Model == null) return "Unknown";
                
                var total = Model.TotalPackages;
                var thirdParty = Model.ThirdPartyPackages;
                var system = Model.SystemPackages;
                
                if (total > 0)
                {
                    return $"{total} total ({thirdParty} user, {system} system)";
                }
                
                return "Unknown";
            }
        }
        
        // Last Updated
        public string LastUpdated => Model?.LastUpdatedText ?? "Unknown";
        
        public override string ToString()
        {
            return DisplayName;
        }
    }
}