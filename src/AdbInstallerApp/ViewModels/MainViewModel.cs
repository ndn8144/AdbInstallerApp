using AdbInstallerApp.Helpers;
using AdbInstallerApp.Models;
using AdbInstallerApp.Services;
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

        private void OnDevicesChanged(List<DeviceInfo> devices)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Devices.Clear();
                foreach (var d in devices)
                {
                    Devices.Add(new DeviceViewModel(d));
                }
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