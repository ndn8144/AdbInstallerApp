using AdbInstallerApp.Helpers;
using AdbInstallerApp.Models;
using AdbInstallerApp.Services;
using AdbInstallerApp.Views;
using CommunityToolkit.Mvvm.ComponentModel;// cspell:disable-line
using CommunityToolkit.Mvvm.Input;// cspell:disable-line
using Ookii.Dialogs.Wpf; // cspell:disable-line
using System.Collections.ObjectModel;
using System.Windows;

namespace AdbInstallerApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly AppSettings _settings = null!;
        private readonly AdbService _adb = null!;
        private readonly DeviceMonitor _monitor = null!;
        private readonly ApkRepoIndexer _repo = null!;
        private readonly InstallOrchestrator _installer = null!;

        public ObservableCollection<DeviceViewModel> Devices { get; } = new();
        public ObservableCollection<ApkItemViewModel> ApkFiles { get; } = new();
        public ObservableCollection<ApkGroupViewModel> ApkGroups { get; } = new();

        [ObservableProperty]
        private string _apkRepoPath = string.Empty;

        [ObservableProperty]
        private bool _optReinstall = true;

        [ObservableProperty]
        private bool _optGrantPerms = false;

        [ObservableProperty]
        private bool _optDowngrade = false;

        [ObservableProperty]
        private string _logText = string.Empty;
        
        [ObservableProperty]
        private bool _isGroupViewEnabled = false;
        
        partial void OnIsGroupViewEnabledChanged(bool value)
        {
            // Trigger property change for dependent properties
            OnPropertyChanged(nameof(InstallButtonTooltip));
        }
        
        [ObservableProperty]
        private bool _enableParallelInstall = false;
        
        [ObservableProperty]
        private int _maxConcurrentDevices = 3;
        
        [ObservableProperty]
        private bool _autoSelectOptimalSplits = true;
        
        [ObservableProperty]
        private bool _showCompatibilityWarnings = true;
        
        [ObservableProperty]
        private string _adbPath = "";
        
        [ObservableProperty]
        private string _aaptPath = "";
        
        [ObservableProperty]
        private bool _enableDetailedLogging = false;
        
        [ObservableProperty]
        private int _logRetentionDays = 7;

        // Device status summary for UI display
        public string DeviceStatusSummary
        {
            get
            {
                if (Devices.Count == 0)
                    return "No devices connected";
                
                var ready = Devices.Count(d => d.DebugInfo.Status == Models.UsbDebugStatus.Ready);
                var unauthorized = Devices.Count(d => d.DebugInfo.Status == Models.UsbDebugStatus.NeedAuthorize);
                var offline = Devices.Count(d => d.DebugInfo.Status == Models.UsbDebugStatus.Offline);
                
                var summary = $"📱 {Devices.Count} device(s): ";
                if (ready > 0) summary += $"🟢 {ready} ready, ";
                if (unauthorized > 0) summary += $"🟡 {unauthorized} need auth, ";
                if (offline > 0) summary += $"🔴 {offline} offline";
                
                return summary.TrimEnd(',', ' ');
            }
        }

        public MainViewModel()
        {
            try
            {
                AppendLog("🔧 Initializing ADB APK Installer...");
                
                _settings = JsonSettings.Load();
                AppendLog("✅ Settings loaded successfully");
                
                _adb = new AdbService();
                AppendLog("✅ ADB service initialized");
                
                _monitor = new DeviceMonitor(_adb);
                _repo = new ApkRepoIndexer();
                _installer = new InstallOrchestrator(_adb);
                AppendLog("✅ All services initialized");

                ApkRepoPath = _settings.ApkRepoPath;
                OptReinstall = _settings.Reinstall;
                OptGrantPerms = _settings.GrantPerms;
                OptDowngrade = _settings.Downgrade;
                EnableParallelInstall = _settings.EnableParallelInstall;
                MaxConcurrentDevices = _settings.MaxConcurrentDevices;
                AutoSelectOptimalSplits = _settings.AutoSelectOptimalSplits;
                ShowCompatibilityWarnings = _settings.ShowCompatibilityWarnings;
                AdbPath = _settings.AdbPath;
                AaptPath = _settings.AaptPath;
                EnableDetailedLogging = _settings.EnableDetailedLogging;
                LogRetentionDays = _settings.LogRetentionDays;

                _monitor.DevicesChanged += OnDevicesChanged;
                _monitor.DeviceConnected += OnDeviceConnected;
                _monitor.DeviceDisconnected += OnDeviceDisconnected;
                _monitor.DeviceStateChanged += OnDeviceStateChanged;
                _monitor.Start();
                AppendLog("✅ Device monitoring started");
                
                _repo.SetRepo(ApkRepoPath);
                RebuildApkList();
                RebuildApkGroups();
                AppendLog("✅ APK repository indexed");

                _ = _adb.StartServerAsync();
                AppendLog("🚀 ADB server starting...");
                AppendLog("📱 Ready! Connect your Android devices and select APK files to install.");
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Initialization error: {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to initialize application: {ex.Message}", 
                    "Initialization Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void OnDevicesChanged(List<DeviceInfo> devices)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var currentCount = Devices.Count;
                    var newCount = devices.Count;
                    
                    // Clear and rebuild if count changed significantly
                    if (Math.Abs(currentCount - newCount) > 0)
            {
                Devices.Clear();
                foreach (var d in devices)
                {
                    Devices.Add(new DeviceViewModel(d));
                }
                        
                        AppendLog($"[DEVICE] Device list updated: {newCount} device(s)");
                        OnPropertyChanged(nameof(DeviceStatusSummary));
                    }
                    else
                    {
                        // Update existing devices if state changed
                        var updated = false;
                        for (int i = 0; i < Math.Min(Devices.Count, devices.Count); i++)
                        {
                            if (Devices[i].State != devices[i].State)
                            {
                                Devices[i] = new DeviceViewModel(devices[i]);
                                updated = true;
                            }
                        }
                        
                        if (updated)
                        {
                            AppendLog($"[DEVICE] Device states updated");
                            OnPropertyChanged(nameof(DeviceStatusSummary));
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] Failed to update device list: {ex.Message}");
            }
        }

        private void OnDeviceConnected(DeviceInfo device)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var deviceName = !string.IsNullOrEmpty(device.Model) ? device.Model : device.Product;
                AppendLog($"📱 Device connected: {deviceName} ({device.Serial})");
                OnPropertyChanged(nameof(DeviceStatusSummary));
            });
        }

        private void OnDeviceDisconnected(DeviceInfo device)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var deviceName = !string.IsNullOrEmpty(device.Model) ? device.Model : device.Product;
                AppendLog($"❌ Device disconnected: {deviceName} ({device.Serial})");
                OnPropertyChanged(nameof(DeviceStatusSummary));
            });
        }

        private void OnDeviceStateChanged(DeviceInfo device)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var deviceName = !string.IsNullOrEmpty(device.Model) ? device.Model : device.Product;
                AppendLog($"🔄 Device state changed: {deviceName} -> {device.State}");
                OnPropertyChanged(nameof(DeviceStatusSummary));
            });
        }

        private void RebuildApkList()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ApkFiles.Clear();
                foreach (var item in _repo.Items)
                {
                    ApkFiles.Add(new ApkItemViewModel(item));
                }
            });
        }

        private void RebuildApkGroups()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ApkGroups.Clear();
                foreach (var group in _repo.ApkGroups)
                {
                    ApkGroups.Add(new ApkGroupViewModel(group));
                }
            });
        }

        [RelayCommand]
        private void ChooseRepo()
        {
            var dlg = new VistaFolderBrowserDialog();
            dlg.ShowNewFolderButton = false;
            if (dlg.ShowDialog() == true)
            {
                ApkRepoPath = dlg.SelectedPath;
                _repo.SetRepo(ApkRepoPath);
                RebuildApkList();
            }
        }

        [RelayCommand]
        private void RefreshRepo()
        {
            _repo.Refresh();
            RebuildApkList();
            RebuildApkGroups();
        }
        
        [RelayCommand]
        private async Task RefreshDevices()
        {
            try
            {
                AppendLog("[DEVICE] Manually refreshing device list...");
                var devices = await _adb.ListDevicesAsync();
                OnDevicesChanged(devices);
                AppendLog($"[DEVICE] Found {devices.Count} device(s)");
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] Failed to refresh devices: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task InstallOnSelectedAsync()
        {
            var selDevices = Devices.Where(d => d.IsSelected).Select(d => d.Model).ToList();
            var apksToInstall = ApksForInstallation;
            
            if (selDevices.Count == 0) { AppendLog("[INFO] No device selected."); return; }
            if (apksToInstall.Count == 0) { AppendLog("[INFO] No APK or APK group selected."); return; }

            AppendLog($"Starting install on {selDevices.Count} device(s) ...");
            
            if (IsGroupViewEnabled)
            {
                var selGroups = ApkGroups.Where(g => g.IsSelected).Select(g => g.Model).ToList();
                AppendLog($"[INFO] Installing {selGroups.Count} APK group(s) with {apksToInstall.Count} total APK(s)...");
                await _installer.InstallGroupsAsync(selDevices, selGroups, OptReinstall, OptGrantPerms, OptDowngrade, AppendLog);
            }
            else
            {
                AppendLog($"[INFO] Installing {apksToInstall.Count} individual APK(s)...");
                await _installer.InstallAsync(selDevices, apksToInstall, OptReinstall, OptGrantPerms, OptDowngrade, AppendLog);
            }
        }

        // Helper method to get all APKs from selected groups
        private List<ApkItem> GetAllApksFromSelectedGroups()
        {
            var allApks = new List<ApkItem>();
            var selectedGroups = ApkGroups.Where(g => g.IsSelected).ToList();
            
            foreach (var group in selectedGroups)
            {
                if (group.Model.BaseApk != null)
                    allApks.Add(group.Model.BaseApk);
                allApks.AddRange(group.Model.SplitApks);
            }
            
            return allApks;
        }

        // Property to get APKs for installation based on current view mode
        public List<ApkItem> ApksForInstallation
        {
            get
            {
                if (IsGroupViewEnabled)
                {
                    // In Group View, get APKs from selected groups
                    return GetAllApksFromSelectedGroups();
                }
                else
                {
                    // In List View, get selected individual APKs
                    return ApkFiles.Where(a => a.IsSelected).Select(a => a.Model).ToList();
                }
            }
        }

        // Tooltip for install button based on current view mode
        public string InstallButtonTooltip
        {
            get
            {
                var deviceCount = Devices.Count(d => d.IsSelected);
                var apkCount = ApksForInstallation.Count;
                
                if (deviceCount == 0)
                    return "Please select at least one device";
                
                if (apkCount == 0)
                {
                    if (IsGroupViewEnabled)
                        return "Please select at least one APK group";
                    else
                        return "Please select at least one APK file";
                }
                
                if (IsGroupViewEnabled)
                {
                    var groupCount = ApkGroups.Count(g => g.IsSelected);
                    return $"Install {groupCount} group(s) with {apkCount} APK(s) on {deviceCount} device(s)";
                }
                else
                {
                    return $"Install {apkCount} APK(s) on {deviceCount} device(s)";
                }
            }
        }

        // APK Group Management Commands
        [RelayCommand]
        private void EditGroup()
        {
            var selectedGroup = ApkGroups.FirstOrDefault(g => g.IsSelected);
            if (selectedGroup == null)
            {
                AppendLog("[INFO] Please select a group to edit.");
                return;
            }

            // Create input dialog for editing group name
            var inputDialog = new InputDialog("Edit APK Group", "Enter new group name:", selectedGroup.DisplayName);
            
            if (inputDialog.ShowDialog() == true)
            {
                var newName = inputDialog.Answer;
                if (string.IsNullOrWhiteSpace(newName))
                {
                    AppendLog("[WARNING] Group name cannot be empty.");
                    return;
                }

                var oldName = selectedGroup.DisplayName;
                selectedGroup.Model.DisplayName = newName;
                
                RebuildApkGroups();
                AppendLog($"[INFO] Renamed group '{oldName}' to '{newName}'.");
            }
        }

        [RelayCommand]
        private void ExportGroup()
        {
            var selectedGroup = ApkGroups.FirstOrDefault(g => g.IsSelected);
            if (selectedGroup == null)
            {
                AppendLog("[INFO] Please select a group to export.");
                return;
            }

            // For now, show export info in log
            AppendLog($"[INFO] Exporting group: {selectedGroup.DisplayName}");
            AppendLog($"[INFO] Total APKs: {1 + selectedGroup.Model.SplitApks.Count}");
            AppendLog($"[INFO] Total size: {selectedGroup.Model.TotalSize} bytes");
            
            // TODO: Implement proper export functionality
            AppendLog("[INFO] Export functionality not yet implemented. Use context menu options instead.");
        }
        
        [RelayCommand]
        private void ExportAllGroups()
        {
            if (ApkGroups.Count == 0)
            {
                AppendLog("[INFO] No groups to export.");
                return;
            }

            var totalApks = ApkGroups.Sum(g => 1 + g.Model.SplitApks.Count);
            var totalSize = ApkGroups.Sum(g => g.Model.TotalSize);
            
            AppendLog($"[INFO] Exporting all {ApkGroups.Count} groups");
            AppendLog($"[INFO] Total APKs: {totalApks}");
            AppendLog($"[INFO] Total size: {totalSize} bytes");
            
            // TODO: Implement proper export functionality
            AppendLog("[INFO] Export functionality not yet implemented.");
        }
        
        [RelayCommand]
        private void ClearAllGroups()
        {
            if (ApkGroups.Count == 0)
            {
                AppendLog("[INFO] No groups to clear.");
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete all {ApkGroups.Count} groups?",
                "Clear All Groups", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                var count = ApkGroups.Count;
                ApkGroups.Clear();
                _repo.ApkGroups.Clear();
                AppendLog($"[INFO] Cleared all {count} groups.");
            }
        }
        
        [RelayCommand]
        private void MoveApkToGroup()
        {
            // This will be implemented when we have the source APK context
            AppendLog("[INFO] Move APK to Group - Select target group first");
        }
        
        [RelayCommand]
        private void RemoveApkFromGroup()
        {
            // This will be implemented when we have the APK context
            AppendLog("[INFO] Remove APK from Group - Select APK first");
        }

        [RelayCommand]
        private void CreateApkGroup()
        {
            var selectedApks = ApkFiles.Where(a => a.IsSelected).ToList();
            if (selectedApks.Count == 0)
            {
                AppendLog("[INFO] Please select APK files to create a group.");
                return;
            }

            // Create a simple input dialog for group name
            var defaultName = $"Group_{DateTime.Now:yyMMdd_HHmm}";
            var inputDialog = new InputDialog("Create APK Group", "Enter group name:", defaultName);
            
            if (inputDialog.ShowDialog() != true) return;
            
            var groupName = inputDialog.Answer;
            if (string.IsNullOrWhiteSpace(groupName))
            {
                AppendLog("[WARNING] Group name cannot be empty.");
                return;
            }

            // Create new group with selected APKs
            var baseApk = selectedApks.FirstOrDefault(a => a.Model.Type == ApkType.Base)?.Model;
            var splitApks = selectedApks.Where(a => a.Model.Type != ApkType.Base).Select(a => a.Model).ToList();

            if (baseApk == null)
            {
                AppendLog("[WARNING] No base APK found in selection. Using first APK as base.");
                baseApk = selectedApks.FirstOrDefault()?.Model ?? new ApkItem();
                splitApks = selectedApks.Skip(1).Select(a => a.Model).ToList();
            }

            var newGroup = new ApkGroup
            {
                PackageName = baseApk.PackageName ?? groupName,
                DisplayName = groupName,
                BaseApk = baseApk,
                SplitApks = splitApks
            };

            _repo.ApkGroups.Add(newGroup);
            RebuildApkGroups();
            AppendLog($"[INFO] Created group '{groupName}' with {selectedApks.Count} APK(s).");
        }

        [RelayCommand]
        private void AddToGroup()
        {
            var selectedApks = ApkFiles.Where(a => a.IsSelected).ToList();
            var selectedGroup = ApkGroups.FirstOrDefault(g => g.IsSelected);
            
            if (selectedApks.Count == 0)
            {
                AppendLog("[INFO] Please select APK files to add to group.");
                return;
            }
            
            if (selectedGroup == null)
            {
                AppendLog("[INFO] Please select a group to add APKs to.");
                return;
            }

            foreach (var apk in selectedApks)
            {
                if (apk.Model.Type == ApkType.Base && selectedGroup.Model.BaseApk == null)
                {
                    selectedGroup.Model.BaseApk = apk.Model;
                }
                else
                {
                    selectedGroup.Model.SplitApks.Add(apk.Model);
                }
            }

            RebuildApkGroups();
            AppendLog($"[INFO] Added {selectedApks.Count} APK(s) to group '{selectedGroup.DisplayName}'.");
        }

        [RelayCommand]
        private void DeleteGroup()
        {
            var selectedGroups = ApkGroups.Where(g => g.IsSelected).ToList();
            if (selectedGroups.Count == 0)
            {
                AppendLog("[INFO] Please select group(s) to delete.");
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete {selectedGroups.Count} group(s)?",
                "Delete APK Groups", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);
                
            if (result != MessageBoxResult.Yes) return;

            foreach (var group in selectedGroups)
            {
                _repo.ApkGroups.Remove(group.Model);
            }

            RebuildApkGroups();
            AppendLog($"[INFO] Deleted {selectedGroups.Count} group(s).");
        }

        // APK File Management Commands
        [RelayCommand]
        private async Task QuickInstallAsync()
        {
            var selectedApks = ApkFiles.Where(a => a.IsSelected).ToList();
            var availableDevices = Devices.Where(d => d.Model.State == "device").ToList();
            
            if (selectedApks.Count == 0)
            {
                AppendLog("[INFO] Please select APK files for quick install.");
                return;
            }
            
            if (availableDevices.Count == 0)
            {
                AppendLog("[INFO] No devices available for quick install.");
                return;
            }

            AppendLog($"[INFO] Quick installing {selectedApks.Count} APK(s) on {availableDevices.Count} device(s)...");
            
            var apkModels = selectedApks.Select(a => a.Model).ToList();
            var deviceModels = availableDevices.Select(d => d.Model).ToList();
            
            await _installer.InstallAsync(deviceModels, apkModels, OptReinstall, OptGrantPerms, OptDowngrade, AppendLog);
        }

        [RelayCommand]
        private void SelectAllApks()
        {
            foreach (var apk in ApkFiles)
            {
                apk.IsSelected = true;
            }
            AppendLog($"[INFO] Selected all {ApkFiles.Count} APK files.");
        }

        [RelayCommand]
        private void ClearApkSelection()
        {
            foreach (var apk in ApkFiles)
            {
                apk.IsSelected = false;
            }
            AppendLog("[INFO] Cleared APK file selection.");
        }
        
        // Settings Commands
        [RelayCommand]
        private void BrowseAdb()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select ADB executable",
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                FileName = "adb.exe"
            };
            
            if (dlg.ShowDialog() == true)
            {
                AdbPath = dlg.FileName;
                AppendLog($"[INFO] ADB path set to: {AdbPath}");
            }
        }
        
        [RelayCommand]
        private void BrowseAapt()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select AAPT executable",
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                FileName = "aapt.exe"
            };
            
            if (dlg.ShowDialog() == true)
            {
                AaptPath = dlg.FileName;
                AppendLog($"[INFO] AAPT path set to: {AaptPath}");
            }
        }
        
        [RelayCommand]
        private void ResetToDefaults()
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all settings to defaults?",
                "Reset Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                // Reset individual properties instead of reassigning readonly field
                var defaultSettings = new AppSettings();
                ApkRepoPath = defaultSettings.ApkRepoPath;
                OptReinstall = defaultSettings.Reinstall;
                OptGrantPerms = defaultSettings.GrantPerms;
                OptDowngrade = defaultSettings.Downgrade;
                EnableParallelInstall = defaultSettings.EnableParallelInstall;
                MaxConcurrentDevices = defaultSettings.MaxConcurrentDevices;
                AutoSelectOptimalSplits = defaultSettings.AutoSelectOptimalSplits;
                ShowCompatibilityWarnings = defaultSettings.ShowCompatibilityWarnings;
                AdbPath = defaultSettings.AdbPath;
                AaptPath = defaultSettings.AaptPath;
                EnableDetailedLogging = defaultSettings.EnableDetailedLogging;
                LogRetentionDays = defaultSettings.LogRetentionDays;
                AppendLog("[INFO] Settings reset to defaults");
            }
        }
        
        private void LoadSettingsFromAppSettings()
        {
            ApkRepoPath = _settings.ApkRepoPath;
            OptReinstall = _settings.Reinstall;
            OptGrantPerms = _settings.GrantPerms;
            OptDowngrade = _settings.Downgrade;
            EnableParallelInstall = _settings.EnableParallelInstall;
            MaxConcurrentDevices = _settings.MaxConcurrentDevices;
            AutoSelectOptimalSplits = _settings.AutoSelectOptimalSplits;
            ShowCompatibilityWarnings = _settings.ShowCompatibilityWarnings;
            AdbPath = _settings.AdbPath;
            AaptPath = _settings.AaptPath;
            EnableDetailedLogging = _settings.EnableDetailedLogging;
            LogRetentionDays = _settings.LogRetentionDays;
        }

        public void AppendLog(string line)
        {
            LogText += (string.IsNullOrEmpty(LogText) ? string.Empty : "\r\n") + line;
            // Ensure UI thread updates
            OnPropertyChanged(nameof(LogText));
        }

        public void PersistSettings()
        {
            _settings.ApkRepoPath = ApkRepoPath;
            _settings.Reinstall = OptReinstall;
            _settings.GrantPerms = OptGrantPerms;
            _settings.Downgrade = OptDowngrade;
            _settings.EnableParallelInstall = EnableParallelInstall;
            _settings.MaxConcurrentDevices = MaxConcurrentDevices;
            _settings.AutoSelectOptimalSplits = AutoSelectOptimalSplits;
            _settings.ShowCompatibilityWarnings = ShowCompatibilityWarnings;
            _settings.AdbPath = AdbPath;
            _settings.AaptPath = AaptPath;
            _settings.EnableDetailedLogging = EnableDetailedLogging;
            _settings.LogRetentionDays = LogRetentionDays;
            JsonSettings.Save(_settings);
        }
        
        // Drag & Drop helper method
        public void AddDroppedApksToGroup(object droppedItems, ApkGroupViewModel targetGroup)
        {
            try
            {
                if (droppedItems is System.Collections.IList items)
                {
                    var apkViewModels = items.OfType<ApkItemViewModel>().ToList();
                    if (apkViewModels.Count > 0)
                    {
                        foreach (var apk in apkViewModels)
                        {
                            if (apk.Model.Type == ApkType.Base && targetGroup.Model.BaseApk == null)
                            {
                                targetGroup.Model.BaseApk = apk.Model;
                                AppendLog($"[INFO] Added base APK: {apk.FileName}");
                            }
                            else
                            {
                                targetGroup.Model.SplitApks.Add(apk.Model);
                                AppendLog($"[INFO] Added split APK: {apk.FileName}");
                            }
                        }
                        
                        RebuildApkGroups();
                        AppendLog($"[SUCCESS] Added {apkViewModels.Count} APK(s) to group '{targetGroup.DisplayName}'");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] Failed to add dropped APKs: {ex.Message}");
            }
        }
    }
}