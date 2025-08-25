using AdbInstallerApp.Helpers;
using AdbInstallerApp.Models;
using AdbInstallerApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;// cspell:disable-line
using CommunityToolkit.Mvvm.Input;// cspell:disable-line
using Ookii.Dialogs.Wpf; // cspell:disable-line
using System.Collections.ObjectModel;
using System.Windows;
using System.ComponentModel;

namespace AdbInstallerApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly AppSettings _settings = null!;
        private readonly AdbService _adb = null!;
        private readonly DeviceMonitor _monitor = null!;
        private readonly ApkRepoIndexer _repo = null!;
        private readonly InstallOrchestrator _installer = null!;
        private System.Threading.Timer? _refreshTimer;

        public ObservableCollection<DeviceViewModel> Devices { get; } = new();
        public ObservableCollection<ApkItemViewModel> ApkFiles { get; } = new();

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
        private string _currentModule = "Welcome";
        
        // Computed properties for display
        public string DevicesCountText => $"{Devices.Count} Devices";
        public string ApkFilesCountText => $"{ApkFiles.Count} APKs";
        public string DeviceStatusSummary => Devices.Count > 0 ? $"{Devices.Count} device(s) connected" : "No devices connected";
        
        // Selection properties
        public int SelectedDevicesCount => Devices.Count(d => d.IsSelected);
        public int SelectedApksCount => ApkFiles.Count(a => a.IsSelected);
        public string SelectedDevicesCountText => SelectedDevicesCount > 0 ? $"{SelectedDevicesCount} device(s) selected" : "No devices selected";
        public string SelectedApksCountText => SelectedApksCount > 0 ? $"{SelectedApksCount} APK(s) selected" : "No APKs selected";
        public string StatusBarText => $"Ready - {Devices.Count} devices, {ApkFiles.Count} APKs";

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

                _monitor.DevicesChanged += OnDevicesChanged;
                _monitor.Start();
                AppendLog("✅ Device monitoring started");
                
                _repo.SetRepo(ApkRepoPath);
                RebuildApkList();
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

        public void Dispose()
        {
            try
            {
                _refreshTimer?.Dispose();
                _refreshTimer = null;
            }
            catch (Exception ex)
            {
                AppendLog($"Error disposing MainViewModel: {ex.Message}");
            }
        }

            private void OnDevicesChanged(List<DeviceInfo> devices)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        Devices.Clear();
                        if (devices != null)
                        {
                            var addedSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            
                            foreach (var d in devices)
                            {
                                if (d != null && !string.IsNullOrEmpty(d.Serial?.Trim()))
                                {
                                    var serial = d.Serial.Trim();
                                    
                                    // Only check serial for uniqueness - this is the most reliable identifier
                                    if (!addedSerials.Contains(serial))
                                    {
                                        var deviceVM = new DeviceViewModel(d);
                                        AppendLog($"Adding device: {deviceVM.DisplayName} (Serial: {serial})");
                                        Devices.Add(deviceVM);
                                        addedSerials.Add(serial);
                                    }
                                    else
                                    {
                                        AppendLog($"Skipping duplicate device with serial: {serial}");
                                    }
                                }
                                else
                                {
                                    AppendLog("Warning: Null or invalid device info received");
                                }
                            }
                        }
                        else
                        {
                            AppendLog("Warning: Null devices list received");
                        }
                        
                        // Trigger all property changes
                        OnPropertyChanged(nameof(DevicesCountText));
                        OnPropertyChanged(nameof(DeviceStatusSummary));
                        OnPropertyChanged(nameof(SelectedDevicesCount));
                        OnPropertyChanged(nameof(SelectedDevicesCountText));
                        OnPropertyChanged(nameof(StatusBarText));
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Error updating devices: {ex.Message}");
                    }
                });
            }

        private void RebuildApkList()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    ApkFiles.Clear();
                    if (_repo?.Items != null)
                    {
                        var addedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        
                        foreach (var item in _repo.Items)
                        {
                            if (item != null && !string.IsNullOrEmpty(item.FilePath?.Trim()))
                            {
                                var filePath = item.FilePath.Trim();
                                
                                // Prevent duplicate APKs by file path only
                                if (!addedFiles.Contains(filePath))
                                {
                                    var apkVM = new ApkItemViewModel(item);
                                    // Subscribe to property changes to update computed properties
                                    apkVM.PropertyChanged += OnApkItemPropertyChanged;
                                    AppendLog($"Adding APK: {apkVM.DisplayInfo} (Size: {apkVM.FileSize})");
                                    ApkFiles.Add(apkVM);
                                    addedFiles.Add(filePath);
                                }
                                else
                                {
                                    AppendLog($"Skipping duplicate APK: {item.FileName}");
                                }
                            }
                            else
                            {
                                AppendLog("Warning: Null or invalid APK item received");
                            }
                        }
                    }
                    else
                    {
                        AppendLog("Warning: No APK repository or items available");
                    }
                    
                    // Use Dispatcher to ensure UI thread safety
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        RefreshComputedProperties();
                    });
                }
                catch (Exception ex)
                {
                    AppendLog($"Error updating APK list: {ex.Message}");
                }
            });
        }

        private void OnApkItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                // When APK item properties change (especially IsSelected), refresh computed properties
                if (e.PropertyName == nameof(ApkItemViewModel.IsSelected))
                {
                    // Use Dispatcher to ensure UI thread safety and avoid crashes
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            RefreshComputedProperties();
                        }
                        catch (Exception ex)
                        {
                            AppendLog($"Error refreshing computed properties: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error in OnApkItemPropertyChanged: {ex.Message}");
            }
        }

        private void RefreshComputedProperties()
        {
            try
            {
                // Cancel any existing timer
                _refreshTimer?.Dispose();
                
                // Create a new timer to debounce rapid property changes
                _refreshTimer = new System.Threading.Timer(_ =>
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            OnPropertyChanged(nameof(ApkFilesCountText));
                            OnPropertyChanged(nameof(SelectedApksCount));
                            OnPropertyChanged(nameof(SelectedApksCountText));
                            OnPropertyChanged(nameof(StatusBarText));
                        }
                        catch (Exception ex)
                        {
                            AppendLog($"Error in RefreshComputedProperties timer callback: {ex.Message}");
                        }
                    });
                }, null, 100, Timeout.Infinite); // 100ms debounce delay
            }
            catch (Exception ex)
            {
                AppendLog($"Error in RefreshComputedProperties: {ex.Message}");
            }
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
        }

        [RelayCommand]
        private void SelectAllDevices()
        {
            foreach (var device in Devices)
            {
                device.IsSelected = true;
            }
            OnPropertyChanged(nameof(SelectedDevicesCount));
            OnPropertyChanged(nameof(SelectedDevicesCountText));
        }

        [RelayCommand]
        private void ClearDeviceSelection()
        {
            foreach (var device in Devices)
            {
                device.IsSelected = false;
            }
            OnPropertyChanged(nameof(SelectedDevicesCount));
            OnPropertyChanged(nameof(SelectedDevicesCountText));
        }

        [RelayCommand]
        private void SelectAllApks()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var apk in ApkFiles)
                    {
                        if (apk != null)
                        {
                            apk.IsSelected = true;
                        }
                    }
                });
                
                // Use Dispatcher to ensure UI thread safety
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        RefreshComputedProperties();
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Error refreshing properties after SelectAll: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                AppendLog($"Error in SelectAllApks: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ClearApkSelection()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var apk in ApkFiles)
                    {
                        if (apk != null)
                        {
                            apk.IsSelected = false;
                        }
                    }
                });
                
                // Use Dispatcher to ensure UI thread safety
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        RefreshComputedProperties();
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Error refreshing properties after Clear: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                AppendLog($"Error in ClearApkSelection: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task InstallOnSelectedAsync()
        {
            var selDevices = Devices.Where(d => d.IsSelected).Select(d => d.Model).ToList();
            var selApks = ApkFiles.Where(a => a.IsSelected).Select(a => a.Model).ToList();// cspell:disable-line
            if (selDevices.Count == 0) { AppendLog("[INFO] No device selected."); return; }
            if (selApks.Count == 0) { AppendLog("[INFO] No APK selected."); return; }// cspell:disable-line

            AppendLog($"Starting install on {selDevices.Count} device(s) ...");
            await _installer.InstallAsync(selDevices, selApks, OptReinstall, OptGrantPerms, OptDowngrade, AppendLog);// cspell:disable-line
        }

        private void AppendLog(string line)
        {
            LogText += (string.IsNullOrEmpty(LogText) ? string.Empty : "\r\n") + line;
        }

        public void PersistSettings()
        {
            _settings.ApkRepoPath = ApkRepoPath;
            _settings.Reinstall = OptReinstall;
            _settings.GrantPerms = OptGrantPerms;
            _settings.Downgrade = OptDowngrade;
            JsonSettings.Save(_settings);
        }
        
        // Navigation Commands
        [RelayCommand]
        private void NavigateToDevices() => CurrentModule = "Devices";
        
        [RelayCommand]
        private void NavigateToApkFiles() => CurrentModule = "ApkFiles";
        
        [RelayCommand]
        private void NavigateToInstallQueue() => CurrentModule = "InstallQueue";
        
        [RelayCommand]
        private void NavigateToDeviceTools() => CurrentModule = "DeviceTools";
        
        [RelayCommand]
        private void NavigateToWifiAdb() => CurrentModule = "WifiAdb";
        
        [RelayCommand]
        private void NavigateToScreenshots() => CurrentModule = "Screenshots";
        
        [RelayCommand]
        private void NavigateToLogcat() => CurrentModule = "Logcat";
        
        [RelayCommand]
        private void NavigateToInstalledApps() => CurrentModule = "InstalledApps";
        
        [RelayCommand]
        private void NavigateToReports() => CurrentModule = "Reports";
        
        [RelayCommand]
        private void NavigateToSettings() => CurrentModule = "Settings";
        
        // Additional Commands
        [RelayCommand]
        private void RefreshAll()
        {
            RefreshRepo();
            // Add other refresh logic here
        }
        
        [RelayCommand]
        private void ShowStatus()
        {
            AppendLog("[INFO] Application Status: Ready");
        }
        
        [RelayCommand]
        private void ShowHelp()
        {
            AppendLog("[INFO] Help: Select a module from the navigation panel to get started");
        }
        
        [RelayCommand]
        private void SaveSettings()
        {
            PersistSettings();
            AppendLog("[INFO] Settings saved successfully");
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
                var defaultSettings = new AppSettings();
                ApkRepoPath = defaultSettings.ApkRepoPath;
                OptReinstall = defaultSettings.Reinstall;
                OptGrantPerms = defaultSettings.GrantPerms;
                OptDowngrade = defaultSettings.Downgrade;
                AppendLog("[INFO] Settings reset to defaults");
            }
        }
    }
}