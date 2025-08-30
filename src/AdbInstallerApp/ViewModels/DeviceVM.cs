using AdbInstallerApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace AdbInstallerApp.ViewModels
{
    public class DeviceVM : ObservableObject
    {
        private readonly DeviceInfo _model;
        private bool _isSelected;

        public DeviceVM(DeviceInfo model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // Basic Properties
        public string Serial => _model.Serial ?? "Unknown";
        public string Model => _model.Model ?? "Unknown Device";
        public string Manufacturer => _model.Manufacturer ?? "";
        public string State => _model.State ?? "Unknown";
        public string Abi => _model.Abi ?? "Unknown";
        public string Density => _model.Density ?? "Unknown";
        public string Sdk => _model.Sdk ?? "Unknown";
        public string AndroidVersion => _model.AndroidVersion ?? "Unknown";

        // State Color for Badge
        public Brush StateColor
        {
            get
            {
                return State?.ToLower() switch
                {
                    "device" => new SolidColorBrush(Color.FromRgb(34, 197, 94)), // Green
                    "unauthorized" => new SolidColorBrush(Color.FromRgb(249, 115, 22)), // Orange
                    "offline" => new SolidColorBrush(Color.FromRgb(156, 163, 175)), // Gray
                    _ => new SolidColorBrush(Color.FromRgb(156, 163, 175)) // Gray default
                };
            }
        }

        // USB Debug Checklist Tooltip
        public string UsbChecklist
        {
            get
            {
                var state = State?.ToLower();
                return state switch
                {
                    "device" => "✅ Device ready for installation",
                    "unauthorized" => "⚠️ Please authorize USB debugging on device",
                    "offline" => "❌ Device offline - check USB connection",
                    _ => "❓ Unknown device state"
                };
            }
        }

        // Display Properties
        public string DisplayName => !string.IsNullOrEmpty(Manufacturer) && !string.IsNullOrEmpty(Model) 
            ? $"{Manufacturer} {Model}" 
            : Model;

        public string DeviceDetails => $"API {Sdk} • {Abi} • {Density}dpi";

        // Filtering Support
        public bool Matches(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;

            var lowerFilter = filter.ToLower();
            return Serial.ToLower().Contains(lowerFilter) ||
                   Model.ToLower().Contains(lowerFilter) ||
                   Manufacturer.ToLower().Contains(lowerFilter) ||
                   State.ToLower().Contains(lowerFilter) ||
                   Abi.ToLower().Contains(lowerFilter);
        }

        // Device State Checks
        public bool IsOnline => State?.ToLower() == "device";
        public bool IsUnauthorized => State?.ToLower() == "unauthorized";
        public bool IsOffline => State?.ToLower() == "offline";

        // Update model and refresh properties
        public void UpdateModel(DeviceInfo newModel)
        {
            if (newModel == null) return;

            var field = typeof(DeviceVM).GetField("_model", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(this, newModel);

            // Refresh all computed properties
            OnPropertyChanged(nameof(Serial));
            OnPropertyChanged(nameof(Model));
            OnPropertyChanged(nameof(Manufacturer));
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(Abi));
            OnPropertyChanged(nameof(Density));
            OnPropertyChanged(nameof(Sdk));
            OnPropertyChanged(nameof(AndroidVersion));
            OnPropertyChanged(nameof(StateColor));
            OnPropertyChanged(nameof(UsbChecklist));
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(DeviceDetails));
            OnPropertyChanged(nameof(IsOnline));
            OnPropertyChanged(nameof(IsUnauthorized));
            OnPropertyChanged(nameof(IsOffline));
        }
    }
}
