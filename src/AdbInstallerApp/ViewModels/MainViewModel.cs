using AdbInstallerApp.Helpers;
using AdbInstallerApp.Models;
using AdbInstallerApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;// cspell:disable-line
using CommunityToolkit.Mvvm.Input;// cspell:disable-line
using Ookii.Dialogs.Wpf; // cspell:disable-line
using System.Collections.ObjectModel;
using System.Windows;
using System.ComponentModel;
using System.IO;
using Newtonsoft.Json;

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
        private string _currentModule = "Welcome";
        
        [ObservableProperty]
        private bool _showGroupsView = false;
        
        [ObservableProperty]
        private ApkGroupViewModel? _selectedGroup;
        
        [ObservableProperty]
        private string _newGroupName = string.Empty;
        
        [ObservableProperty]
        private string _newGroupDescription = string.Empty;
        
        // Computed properties for display
        public string DevicesCountText => $"{Devices.Count} Devices";
        public string ApkFilesCountText => $"{ApkFiles.Count} APKs";
        public string ApkGroupsCountText => $"{ApkGroups.Count} Groups";
        public string DeviceStatusSummary => Devices.Count > 0 ? $"{Devices.Count} device(s) connected" : "No devices connected";
        
        // APK Classification properties
        public int BaseApkCount => ApkFiles.Count(a => a.Model?.SplitTag == "Base" || string.IsNullOrEmpty(a.Model?.SplitTag));
        public int SplitApkCount => ApkFiles.Count(a => !string.IsNullOrEmpty(a.Model?.SplitTag) && a.Model.SplitTag != "Base");
        public string ApkClassificationText => $"{BaseApkCount} Base, {SplitApkCount} Split";
        
        // Selection properties
        public int SelectedDevicesCount => Devices.Count(d => d.IsSelected);
        public int SelectedApksCount => ApkFiles.Count(a => a.IsSelected) + ApkGroups.SelectMany(g => g.ApkItems).Count(a => a.IsSelected);
        public string SelectedDevicesCountText => SelectedDevicesCount > 0 ? $"{SelectedDevicesCount} device(s) selected" : "No devices selected";
        public string SelectedApksCountText => SelectedApksCount > 0 ? $"{SelectedApksCount} APK(s) selected" : "No APKs selected";
        public string StatusBarText => $"Ready - {Devices.Count} devices, {ApkFiles.Count} APKs, {ApkGroups.Count} groups ({ApkClassificationText})";

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
                LoadApkGroups();
                AppendLog("✅ APK repository indexed");

                AppendLog($"📊 Loaded {ApkGroups.Count} groups");
                foreach (var group in ApkGroups)
                {
                    AppendLog($"  - Group: {group.Name} with {group.ApkItems.Count} APKs");
                }

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
                SaveApkGroups();
                
                // Dispose APK group ViewModels
                foreach (var group in ApkGroups)
                {
                    group.Dispose();
                }
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
                    
                    RefreshComputedProperties();
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
                                
                                if (!addedFiles.Contains(filePath))
                                {
                                    var apkVM = new ApkItemViewModel(item);
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
                if (e.PropertyName == nameof(ApkItemViewModel.IsSelected))
                {
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
                _refreshTimer?.Dispose();
                
                _refreshTimer = new System.Threading.Timer(_ =>
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            OnPropertyChanged(nameof(DevicesCountText));
                            OnPropertyChanged(nameof(ApkFilesCountText));
                            OnPropertyChanged(nameof(ApkGroupsCountText));
                            OnPropertyChanged(nameof(DeviceStatusSummary));
                            OnPropertyChanged(nameof(BaseApkCount));
                            OnPropertyChanged(nameof(SplitApkCount));
                            OnPropertyChanged(nameof(ApkClassificationText));
                            OnPropertyChanged(nameof(SelectedDevicesCount));
                            OnPropertyChanged(nameof(SelectedApksCount));
                            OnPropertyChanged(nameof(SelectedDevicesCountText));
                            OnPropertyChanged(nameof(SelectedApksCountText));
                            OnPropertyChanged(nameof(StatusBarText));
                        }
                        catch (Exception ex)
                        {
                            AppendLog($"Error in RefreshComputedProperties timer callback: {ex.Message}");
                        }
                    });
                }, null, 100, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                AppendLog($"Error in RefreshComputedProperties: {ex.Message}");
            }
        }

        // // APK Group Management
        // [RelayCommand]
        // private void CreateGroup()
        // {
        //     if (string.IsNullOrWhiteSpace(NewGroupName))
        //     {
        //         MessageBox.Show("Please enter a group name.", "Create Group", MessageBoxButton.OK, MessageBoxImage.Warning);
        //         return;
        //     }
            
        //     if (ApkGroups.Any(g => g.Name.Equals(NewGroupName.Trim(), StringComparison.OrdinalIgnoreCase)))
        //     {
        //         MessageBox.Show("A group with this name already exists.", "Create Group", MessageBoxButton.OK, MessageBoxImage.Warning);
        //         return;
        //     }
            
        //     try
        //     {
        //         var group = new ApkGroup(NewGroupName.Trim(), NewGroupDescription.Trim());
        //         var groupViewModel = new ApkGroupViewModel(group);
        //         ApkGroups.Add(groupViewModel);
                
        //         NewGroupName = string.Empty;
        //         NewGroupDescription = string.Empty;
                
        //         AppendLog($"✅ Created group: {group.Name}");
        //         RefreshComputedProperties();
        //         SaveApkGroups();
        //     }
        //     catch (Exception ex)
        //     {
        //         AppendLog($"❌ Error creating group: {ex.Message}");
        //         MessageBox.Show($"Error creating group: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        //     }
        // }
        
        [RelayCommand]
        private void DeleteGroup(ApkGroupViewModel? group)
        {
            if (group == null) return;
            
            var result = MessageBox.Show(
                $"Are you sure you want to delete the group '{group.Name}'?\n\nThis will not delete the APK files, only remove them from the group.",
                "Delete Group",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    group.Dispose();
                    ApkGroups.Remove(group);
                    if (SelectedGroup == group)
                    {
                        SelectedGroup = null;
                    }
                    
                    AppendLog($"🗑️ Deleted group: {group.Name}");
                    RefreshComputedProperties();
                    SaveApkGroups();
                }
                catch (Exception ex)
                {
                    AppendLog($"❌ Error deleting group: {ex.Message}");
                }
            }
        }
        
        [RelayCommand]
        private void AddSelectedApksToGroup(ApkGroupViewModel? group)
        {
            if (group == null) return;
            
            var selectedApks = ApkFiles.Where(a => a.IsSelected).ToList();
            if (selectedApks.Count == 0)
            {
                MessageBox.Show("Please select some APK files to add to the group.", "Add to Group", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            try
            {
                int added = 0;
                foreach (var apk in selectedApks)
                {
                    if (!group.Model.ContainsApk(apk.Model))
                    {
                        group.AddApk(apk);
                        added++;
                    }
                }
                
                AppendLog($"➕ Added {added} APK(s) to group '{group.Name}'");
                RefreshComputedProperties();
                SaveApkGroups();
                
                if (added < selectedApks.Count)
                {
                    MessageBox.Show($"Added {added} APK(s) to the group. {selectedApks.Count - added} were already in the group.", 
                        "Add to Group", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Error adding APKs to group: {ex.Message}");
            }
        }
        
        [RelayCommand]
        private void RemoveApkFromGroup(object? parameter)
        {
            if (parameter is not object[] parameters || parameters.Length != 2)
                return;
                
            if (parameters[0] is not ApkGroupViewModel group || parameters[1] is not ApkItemViewModel apk)
                return;
                
            try
            {
                group.RemoveApk(apk);
                AppendLog($"➖ Removed '{apk.DisplayInfo}' from group '{group.Name}'");
                RefreshComputedProperties();
                SaveApkGroups();
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Error removing APK from group: {ex.Message}");
            }
        }
        
        [RelayCommand]
        private void ToggleGroupsView()
        {
            ShowGroupsView = !ShowGroupsView;
            AppendLog($"📋 Switched to {(ShowGroupsView ? "Groups" : "List")} view");
        }

        // Existing commands
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
            AppendLog("🔄 Refreshing APK repository...");
            _repo.Refresh();
            RebuildApkList();
            AppendLog($"✅ Repository refreshed. Found {ApkFiles.Count} APK files.");
            AppendLog($"📊 Classification: {BaseApkCount} Base APKs, {SplitApkCount} Split APKs");
        }

        [RelayCommand]
        private void SelectAllDevices()
        {
            foreach (var device in Devices)
            {
                device.IsSelected = true;
            }
            RefreshComputedProperties();
        }

        [RelayCommand]
        private void ClearDeviceSelection()
        {
            foreach (var device in Devices)
            {
                device.IsSelected = false;
            }
            RefreshComputedProperties();
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
                    
                    // Also select APKs in groups if in groups view
                    if (ShowGroupsView)
                    {
                        foreach (var group in ApkGroups)
                        {
                            group.SelectAllApks();
                        }
                    }
                });
                
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
                    
                    // Also clear selection in groups
                    foreach (var group in ApkGroups)
                    {
                        group.ClearApkSelection();
                    }
                });
                
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
            var selApks = GetSelectedApks();
            
            if (selDevices.Count == 0) 
            { 
                AppendLog("[INFO] No device selected."); 
                return; 
            }
            
            if (selApks.Count == 0) 
            { 
                AppendLog("[INFO] No APK selected."); 
                return; 
            }

            AppendLog($"Starting install on {selDevices.Count} device(s) with {selApks.Count} APK(s)...");
            await _installer.InstallAsync(selDevices, selApks, OptReinstall, OptGrantPerms, OptDowngrade, AppendLog);
        }
        
        private List<ApkItem> GetSelectedApks()
        {
            var selectedApks = new List<ApkItem>();
            
            // Add selected APKs from main list
            selectedApks.AddRange(ApkFiles.Where(a => a.IsSelected).Select(a => a.Model));
            
            // Add selected APKs from groups
            foreach (var group in ApkGroups)
            {
                selectedApks.AddRange(group.ApkItems.Where(a => a.IsSelected).Select(a => a.Model));
            }
            
            return selectedApks.Distinct().ToList();
        }

        public void AppendLog(string line)
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
            SaveApkGroups();
        }
        
        // APK Groups persistence
        private void LoadApkGroups()
        {
            try
            {
                var groupsPath = GetGroupsFilePath();
                AppendLog($"🔍 Loading groups from: {groupsPath}");
                
                if (File.Exists(groupsPath))
                {
                    var json = File.ReadAllText(groupsPath);
                    var groups = JsonConvert.DeserializeObject<List<ApkGroup>>(json);
                    
                    if (groups != null)
                    {
                        ApkGroups.Clear();
                        foreach (var group in groups)
                        {
                            var groupViewModel = new ApkGroupViewModel(group);
                            ApkGroups.Add(groupViewModel);
                            AppendLog($"  ✅ Loaded group: {group.Name} with {group.ApkItems?.Count ?? 0} APKs");
                        }
                        AppendLog($"✅ Loaded {groups.Count} APK group(s)");
                        
                        // Auto-switch to groups view if groups exist
                        if (groups.Count > 0 && CurrentModule == "ApkFiles")
                        {
                            ShowGroupsView = true;
                            OnPropertyChanged(nameof(ShowGroupsView));
                            AppendLog("🔄 Auto-switched to Groups View");
                        }
                    }
                    else
                    {
                        AppendLog("⚠️ Groups file exists but deserialization returned null");
                    }
                }
                else
                {
                    AppendLog("⚠️ Groups file not found, starting with empty groups");
                }
                
                // Debug: Log trạng thái hiện tại
                AppendLog($" Current state: {ApkGroups.Count} groups in collection");
                
                // Force UI update đầy đủ
                OnPropertyChanged(nameof(ApkGroups));
                OnPropertyChanged(nameof(ShowGroupsView));
                OnPropertyChanged(nameof(ApkGroupsCountText));
                OnPropertyChanged(nameof(StatusBarText));
                
                // Force refresh computed properties
                RefreshComputedProperties();
            }
            catch (Exception ex)
            {
                AppendLog($"⚠️ Error loading APK groups: {ex.Message}");
            }
        }
        
        private void SaveApkGroups()
        {
            try
            {
                var groups = ApkGroups.Select(g => g.Model).ToList();
                var json = JsonConvert.SerializeObject(groups, Formatting.Indented);
                var groupsPath = GetGroupsFilePath();
                
                var dir = Path.GetDirectoryName(groupsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                
                File.WriteAllText(groupsPath, json);
            }
            catch (Exception ex)
            {
                AppendLog($"⚠️ Error saving APK groups: {ex.Message}");
            }
        }
        
        private string GetGroupsFilePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AdbInstallerApp", "apk-groups.json");
        }
        
        // Navigation Commands (existing)
        [RelayCommand]
        private void NavigateToDevices() => CurrentModule = "Devices";
        
        [RelayCommand]
        private void NavigateToApkFiles() 
        {
            CurrentModule = "ApkFiles";
            
            // Đảm bảo groups view được hiển thị nếu có groups
            if (ApkGroups.Count > 0)
            {
                ShowGroupsView = true;
                AppendLog($"📋 Switched to APK Files with {ApkGroups.Count} groups");
            }
            else
            {
                ShowGroupsView = false;
                AppendLog("📋 Switched to APK Files (no groups yet)");
            }
            
            // Force UI update đầy đủ
            OnPropertyChanged(nameof(ShowGroupsView));
            OnPropertyChanged(nameof(ApkGroups));
            OnPropertyChanged(nameof(ApkGroupsCountText));
            OnPropertyChanged(nameof(StatusBarText));
            
            // Force refresh computed properties
            RefreshComputedProperties();
        }
        
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
        
        // Additional Commands (existing)
        [RelayCommand]
        private void RefreshAll()
        {
            RefreshRepo();
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

        [ObservableProperty]
        private bool _showCreateGroupDialog = false;

        [ObservableProperty]
        private CreateGroupDialogModel _createGroupDialog = new();

        [RelayCommand]
        private void OpenCreateGroupDialog()
        {
            CreateGroupDialog.Reset();
            ShowCreateGroupDialog = true;
        }

        [RelayCommand]
        private void CreateGroup()
        {
            if (string.IsNullOrWhiteSpace(NewGroupName))
            {
                MessageBox.Show("Please enter a group name.", "Create Group", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (ApkGroups.Any(g => g.Name.Equals(NewGroupName.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("A group with this name already exists.", "Create Group", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                var group = new ApkGroup(NewGroupName.Trim(), NewGroupDescription.Trim());
                var groupViewModel = new ApkGroupViewModel(group);
                ApkGroups.Add(groupViewModel);
                
                NewGroupName = string.Empty;
                NewGroupDescription = string.Empty;
                
                AppendLog($"✅ Created group: {group.Name}");
                
                // Force UI updates
                OnPropertyChanged(nameof(ApkGroups));
                OnPropertyChanged(nameof(ApkGroupsCountText));
                OnPropertyChanged(nameof(StatusBarText));
                
                // Auto-switch to groups view
                ShowGroupsView = true;
                OnPropertyChanged(nameof(ShowGroupsView));
                
                RefreshComputedProperties();
                SaveApkGroups();
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Error creating group: {ex.Message}");
                MessageBox.Show($"Error creating group: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        [RelayCommand]
        private void CancelCreateGroup()
        {
            ShowCreateGroupDialog = false;
        }
    }
}