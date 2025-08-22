using AdbInstallerApp.Models;
using AdbInstallerApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

namespace AdbInstallerApp.ViewModels
{
    public class ApkGroupViewModel : ObservableObject
    {
        private readonly DeviceCompatibilityAnalyzer _compatibilityAnalyzer;
        private readonly ApkGroup _model;

        public ApkGroupViewModel(ApkGroup model)
        {
            _model = model;
            _compatibilityAnalyzer = new DeviceCompatibilityAnalyzer();
        }

        public ApkGroup Model => _model;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                SetProperty(ref _isSelected, value);
                if (value) SmartSelectOptimalSplits();
                else DeselectAllSplits();
                
                // Notify MainViewModel that selection changed
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        // Display properties
        public string PackageName => _model.PackageName;
        public string DisplayName => _model.DisplayName;
        public string DisplayInfo => _model.DisplayInfo;
        public string SplitInfo => _model.SplitInfo;
        public string SizeInfo => _model.SizeInfo;
        public string VersionInfo => $"v{_model.VersionName} ({_model.VersionCode})";
        public string TargetSdkInfo => $"API {_model.TargetSdkVersion}";

        // Split information
        public bool HasSplits => _model.IsSplit;
        public int SplitCount => _model.SplitApks.Count;
        public string SplitDetails => GetSplitDetails();

        // Compatibility (will be updated when devices are available)
        public string CompatibilityText => "Select device to check";
        public bool IsCompatible => true;
        public double CompatibilityScore => 1.0;

        private string GetSplitDetails()
        {
            if (!_model.IsSplit) return "Single APK";

            var details = new List<string>();
            if (_model.AbiSplits.Count > 0) details.Add($"{_model.AbiSplits.Count} ABI");
            if (_model.DpiSplits.Count > 0) details.Add($"{_model.DpiSplits.Count} DPI");
            if (_model.LanguageSplits.Count > 0) details.Add($"{_model.LanguageSplits.Count} Language");
            if (_model.OtherSplits.Count > 0) details.Add($"{_model.OtherSplits.Count} Other");

            return string.Join(", ", details);
        }

        // Update compatibility for a specific device
        public void UpdateCompatibility(DeviceInfo device)
        {
            var result = _compatibilityAnalyzer.CheckCompatibility(device, _model);
            
            // Update properties (this would need to be implemented with proper property change notifications)
            // For now, we'll just return the compatibility result
        }

        // Get all APKs that should be installed for this group
        public List<ApkItem> GetApksForInstallation()
        {
            var apks = new List<ApkItem>();
            
            if (_model.BaseApk != null)
                apks.Add(_model.BaseApk);
                
            // Add all split APKs (in a real implementation, this would be filtered by device compatibility)
            apks.AddRange(_model.SplitApks);
            
            return apks;
        }
        
        private void SmartSelectOptimalSplits()
        {
            // Auto-select base APK
            if (_model.BaseApk != null)
            {
                // Find the corresponding ViewModel and select it
                // This would need to be implemented with proper ViewModel management
            }
            
            // Auto-select best splits for connected devices
            // This would need access to DeviceManager and CompatibilityAnalyzer
            // For now, we'll just log the action
            System.Diagnostics.Debug.WriteLine($"Smart selection enabled for {_model.DisplayName}");
        }
        
        private void DeselectAllSplits()
        {
            // Deselect all split APKs when group is deselected
            // This would need to be implemented with proper ViewModel management
            System.Diagnostics.Debug.WriteLine($"Deselection for {_model.DisplayName}");
        }
    }
}
