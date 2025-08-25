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

        public string Serial => Model?.Serial ?? "Unknown";
        
        public string State => Model?.State ?? "Unknown";
        
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
        
        public string RootStatus => Model?.IsRooted == true ? "🔓 Rooted" : "🔒 Unrooted";
        
        public string Architecture => !string.IsNullOrEmpty(Model?.Abi) ? Model.Abi.ToUpper() : "Unknown";
        
        public string BuildInfo => !string.IsNullOrEmpty(Model?.BuildNumber) ? Model.BuildNumber : "Unknown";
        
        public override string ToString()
        {
            return DisplayName;
        }
    }
}