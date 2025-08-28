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
        private readonly AppInventoryService _appInventory = null!;
        private readonly ApkExportService _apkExporter = null!;
        private readonly DeviceMonitor _monitor = null!;
        private readonly ApkRepoIndexer _repo = null!;
        public readonly InstallOrchestrator _installer = null!;
        private readonly ApkValidationService _apkValidator = null!;
        private readonly MultiGroupInstallOrchestrator _multiGroupInstaller = null!;
        private System.Threading.Timer? _refreshTimer;

        // Multi-Group Install ViewModel
        public MultiGroupInstallViewModel MultiGroupInstall { get; private set; } = null!;

        // Enhanced Services
        public OptimizedProgressService OptimizedProgress { get; } = OptimizedProgressService.Instance;
        public EnhancedInstallQueue InstallQueue { get; private set; } = null!;
        public NotificationService NotificationService { get; } = new();
        public KeyboardShortcutService KeyboardShortcuts { get; private set; } = null!;

        public ObservableCollection<DeviceViewModel> Devices { get; } = new();
        public ObservableCollection<ApkItemViewModel> ApkFiles { get; } = new();
        public ObservableCollection<ApkGroupViewModel> ApkGroups { get; } = new();
        public ObservableCollection<InstalledAppViewModel> InstalledApps { get; } = new();
        public ObservableCollection<InstalledAppViewModel> FilteredInstalledApps { get; } = new();

        [ObservableProperty]
        private bool _isLoadingApps = false;

        [ObservableProperty]
        private bool _showUserAppsOnly = true;

        [ObservableProperty]
        private bool _showSystemApps = false;

        [ObservableProperty]
        private string _appFilterKeyword = string.Empty;

        [ObservableProperty]
        private string _selectedDeviceSerial = string.Empty;

        private DeviceViewModel? _selectedDevice;
        public DeviceViewModel? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                {
                    SelectedDeviceSerial = value?.Model?.Serial ?? string.Empty;
                    OnPropertyChanged(nameof(SelectedDeviceSerial));
                }
            }
        }

        [ObservableProperty]
        private string _exportDirectory = string.Empty;

        [ObservableProperty]
        private bool _exportIncludeSplits = true;

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

        // Installed Apps properties
        public string InstalledAppsCountText => $"{FilteredInstalledApps.Count} Apps";
        public int SelectedInstalledAppsCount => FilteredInstalledApps.Count(a => a.IsSelected);
        public string SelectedInstalledAppsCountText => SelectedInstalledAppsCount > 0 ? $"{SelectedInstalledAppsCount} app(s) selected" : "No apps selected";

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
                _apkValidator = new ApkValidationService();
                _installer = new InstallOrchestrator(_adb);
                _multiGroupInstaller = new MultiGroupInstallOrchestrator(_adb, _installer);

                InstallQueue = new EnhancedInstallQueue(_installer, maxConcurrentOperations: 2);
                KeyboardShortcuts = new KeyboardShortcutService(InstallQueue, OptimizedProgress);

                MultiGroupInstall = new MultiGroupInstallViewModel(_multiGroupInstaller, this);

                ApkGroups.CollectionChanged += (s, e) => MultiGroupInstall.RefreshAvailableGroupsCommand.Execute(null);

                InstallQueue.OperationCompleted += OnInstallOperationCompleted;
                InstallQueue.OperationFailed += OnInstallOperationFailed;
                InstallQueue.QueueEmpty += OnInstallQueueEmpty;

                AppendLog("✅ App inventory, export, and multi-group installation services initialized");

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
                
                // Setup status refresh timer
                SetupStatusRefreshTimer();
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Initialization error: {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to initialize application: {ex.Message}",
                    "Initialization Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void SetupStatusRefreshTimer()
        {
            try
            {
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(3); // Refresh every 3 seconds
                timer.Tick += (sender, e) =>
                {
                    try
                    {
                        // Force refresh device status
                        foreach (var device in Devices)
                        {
                            device.RefreshStatus();
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"❌ Error in status refresh timer: {ex.Message}");
                    }
                };
                timer.Start();
                AppendLog("✅ Status refresh timer started");
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Failed to setup status refresh timer: {ex.Message}");
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
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Devices.Clear();
                foreach (var device in devices)
                {
                    Devices.Add(new DeviceViewModel(device));
                }
                AppendLog($"📱 Devices updated: {Devices.Count} device(s) connected");
            });
        }

        private void OnInstallOperationCompleted(object? sender, EnhancedInstallQueue.InstallQueueEventArgs e)
        {
            var operation = e.Operation;
            var duration = operation.EndTime - operation.StartTime;
            
            NotificationService.ShowCompletionNotification(
                "Installation Complete",
                $"Successfully installed {GetOperationDescription(operation)} in {duration?.TotalSeconds:F1}s",
                NotificationType.Success);
                
            AppendLog($"✅ Installation completed: {GetOperationDescription(operation)}");
        }

        private void OnInstallOperationFailed(object? sender, EnhancedInstallQueue.InstallQueueEventArgs e)
        {
            var operation = e.Operation;
            
            NotificationService.ShowCompletionNotification(
                "Installation Failed",
                $"Failed to install {GetOperationDescription(operation)}: {operation.ErrorMessage}",
                NotificationType.Error);
                
            AppendLog($"❌ Installation failed: {GetOperationDescription(operation)} - {operation.ErrorMessage}");
        }

        private void OnInstallQueueEmpty(object? sender, EnhancedInstallQueue.InstallQueueEventArgs e)
        {
            NotificationService.ShowCompletionNotification(
                "All Installations Complete",
                "All queued installations have been processed",
                NotificationType.Success);
                
            AppendLog("🎉 All installations in queue completed");
        }

        private string GetOperationDescription(EnhancedInstallQueue.QueuedInstallOperation operation)
        {
            return operation.Type switch
            {
                EnhancedInstallQueue.InstallOperationType.SingleApk => $"{operation.ApkItems.Count} APK(s)",
                EnhancedInstallQueue.InstallOperationType.GroupInstall => $"{operation.ApkGroups.Count} group(s)",
                _ => "operation"
            };
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
        private void DeleteSelectedApksFromGroup(object? parameter)
        {
            if (parameter is not ApkGroupViewModel group)
                return;

            try
            {
                var selectedApks = group.ApkItems.Where(a => a.IsSelected).ToList();
                if (selectedApks.Count == 0)
                {
                    AppendLog("ℹ️ No APKs selected for deletion");
                    return;
                }

                foreach (var apk in selectedApks)
                {
                    group.RemoveApk(apk);
                }

                AppendLog($"🗑️ Deleted {selectedApks.Count} APK(s) from group '{group.Name}'");
                RefreshComputedProperties();
                SaveApkGroups();
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Error deleting selected APKs from group: {ex.Message}");
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
        private void DeleteSelectedApks()
        {
            try
            {
                var selectedApks = ApkFiles.Where(a => a.IsSelected).ToList();
                if (selectedApks.Count == 0)
                {
                    AppendLog("ℹ️ No APKs selected for deletion");
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to delete {selectedApks.Count} selected APK(s)?\n\nThis action cannot be undone.",
                    "Delete Selected APKs",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    foreach (var apk in selectedApks)
                    {
                        try
                        {
                            if (File.Exists(apk.Model.FilePath))
                            {
                                File.Delete(apk.Model.FilePath);
                                AppendLog($"🗑️ Deleted file: {apk.DisplayInfo}");
                            }
                        }
                        catch (Exception ex)
                        {
                            AppendLog($"❌ Error deleting {apk.DisplayInfo}: {ex.Message}");
                        }
                    }

                    // Remove from collection
                    foreach (var apk in selectedApks)
                    {
                        ApkFiles.Remove(apk);
                    }

                    // Also remove from groups
                    foreach (var group in ApkGroups)
                    {
                        var apksToRemove = group.ApkItems.Where(a => selectedApks.Any(sa => sa.Model.FilePath == a.Model.FilePath)).ToList();
                        foreach (var apk in apksToRemove)
                        {
                            group.RemoveApk(apk);
                        }
                    }

                    AppendLog($"✅ Successfully deleted {selectedApks.Count} APK file(s)");
                    RefreshComputedProperties();
                    SaveApkGroups();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Error deleting selected APKs: {ex.Message}");
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

        [RelayCommand]
        private void NavigateToMultiGroupInstall() => CurrentModule = "MultiGroupInstall";

        // Additional Commands (existing)
        [RelayCommand]
        private async Task RefreshAll()
        {
            RefreshRepo(); // This is synchronous, no await needed
            await RefreshDevices(); // This is async, needs await to fix CS4014 warning
        }

        [RelayCommand]
        private async Task RefreshDevices()
        {
            AppendLog("🔄 Refreshing device list...");
            try
            {
                // Force refresh devices by calling ADB service directly
                var devices = await _adb.ListDevicesAsync();
                OnDevicesChanged(devices);
                AppendLog($"✅ Device list refreshed. Found {devices.Count} device(s).");
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Error refreshing devices: {ex.Message}");
            }
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

        // Device Management Commands (new)
        [RelayCommand]
        private void ShowUsbDebugChecklist()
        {
            var checklist = @"
🔧 USB Debugging Troubleshooting Checklist:

1. Physical Connection:
   ✓ USB cable is properly connected
   ✓ Try different USB ports
   ✓ Try different USB cables
   ✓ Check for physical damage

2. Device Settings:
   ✓ Developer Options enabled
   ✓ USB Debugging enabled
   ✓ USB Debugging (Security Settings) enabled
   ✓ Verify apps over USB disabled (if needed)

3. Computer Setup:
   ✓ ADB drivers installed
   ✓ ADB server running
   ✓ Device authorized on computer
   ✓ No conflicting software

4. Device Authorization:
   ✓ Check device screen for USB debugging authorization popup
   ✓ Tap 'Allow' when prompted
   ✓ Check 'Always allow from this computer' if desired

5. Troubleshooting Steps:
   ✓ Restart ADB server: adb kill-server && adb start-server
   ✓ Revoke USB debugging authorizations on device
   ✓ Restart device and computer
   ✓ Check Windows Device Manager for device status

6. Advanced:
   ✓ Check device compatibility
   ✓ Verify ADB version compatibility
   ✓ Check for system updates
   ✓ Factory reset as last resort

Current Device Status:
" + string.Join("\n", Devices.Select(d => $"• {d.DisplayName}: {d.ConnectionHealth} - {d.ConnectionDiagnosis}"));

            AppendLog(checklist);
            MessageBox.Show(checklist, "USB Debug Checklist", MessageBoxButton.OK, MessageBoxImage.Information);
        }

[RelayCommand]
        private void ShowDeviceDetails()
        {
            if (Devices.Count == 0)
            {
                MessageBox.Show("No devices connected. Please connect a device first.", "No Devices", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var details = new System.Text.StringBuilder();
            details.AppendLine("📊 Detailed Device Information:");
            details.AppendLine(new string('=', 50));

            foreach (var device in Devices)
            {
                details.AppendLine($"Device: {device.DisplayName}");
                details.AppendLine($"Status: {device.ConnectionStatus} {device.State}");
                details.AppendLine($"Android: {device.DeviceInfo}");
                details.AppendLine($"Hardware: {device.HardwareSummary}");
                details.AppendLine($"Screen: {device.ScreenInfo}");
                details.AppendLine($"Battery: {device.BatteryInfo}");
                details.AppendLine($"Network: {device.NetworkInfo}");
                details.AppendLine($"Root: {device.RootStatus}");
                details.AppendLine($"Security: {device.SecurityStatus}");
                details.AppendLine($"Developer: {device.DeveloperStatus}");
                details.AppendLine($"Health: {device.ConnectionHealth}");
                details.AppendLine($"Packages: {device.PackageSummary}");
                details.AppendLine($"Last Updated: {device.LastUpdated}");
                details.AppendLine();
            }

            AppendLog(details.ToString());
            MessageBox.Show(details.ToString(), "Device Details", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ShowSingleDeviceDetails(object? parameter)
        {
            if (parameter is not DeviceViewModel device)
            {
                MessageBox.Show("Invalid device parameter.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var details = new System.Text.StringBuilder();
            details.AppendLine($"📱 **{device.DisplayName}** - Detailed Information");
            details.AppendLine(new string('=', 60));
            details.AppendLine();
            
            // Basic Information
            details.AppendLine("🔍 **BASIC INFORMATION**");
            details.AppendLine($"   Serial Number: {device.Serial}");
                            details.AppendLine($"   Connection Status: {device.ConnectionStatus} {device.State}");
            details.AppendLine($"   Last Updated: {device.LastUpdated}");
            details.AppendLine();
            
            // Android Information
            details.AppendLine("🤖 **ANDROID SYSTEM**");
            details.AppendLine($"   Version: {device.DeviceInfo}");
            details.AppendLine($"   Build Number: {device.BuildInfo}");
            details.AppendLine($"   Build Type: {device.BuildType}");
            details.AppendLine();
            
            // Hardware Information
            details.AppendLine("⚙️ **HARDWARE SPECIFICATIONS**");
            details.AppendLine($"   Architecture: {device.Architecture}");
            details.AppendLine($"   Hardware Summary: {device.HardwareSummary}");
            details.AppendLine($"   Screen: {device.ScreenInfo}");
            details.AppendLine();
            
            // Battery & Network
            details.AppendLine("🔋 **BATTERY & NETWORK**");
            details.AppendLine($"   Battery: {device.BatteryInfo}");
            details.AppendLine($"   Network: {device.NetworkInfo}");
            details.AppendLine();
            
            // Security & Developer
            details.AppendLine("🔒 **SECURITY & DEVELOPER**");
            details.AppendLine($"   Root Status: {device.RootStatus}");
            details.AppendLine($"   Security: {device.SecurityStatus}");
            details.AppendLine($"   Developer Options: {device.DeveloperStatus}");
            details.AppendLine();
            
            // Connection Health
            details.AppendLine("🏥 **CONNECTION HEALTH**");
            details.AppendLine($"   Health Status: {device.ConnectionHealth}");
            details.AppendLine($"   Diagnosis: {device.ConnectionDiagnosis}");
            details.AppendLine();
            
            // Package Information
            details.AppendLine("📦 **INSTALLED PACKAGES**");
            details.AppendLine($"   Package Summary: {device.PackageSummary}");
            details.AppendLine();

            AppendLog($"📊 Showing detailed information for device: {device.DisplayName}");
            MessageBox.Show(details.ToString(), $"Device Details - {device.DisplayName}", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task RefreshInstalledAppsAsync()
        {
            if (string.IsNullOrEmpty(SelectedDeviceSerial))
            {
                AppendLog("[INFO] No device selected for app refresh");
                return;
            }

            IsLoadingApps = true;
            try
            {
                AppendLog($"Loading installed apps from {SelectedDeviceSerial}...");

                var options = new AppQueryOptions
                {
                    OnlyUserApps = ShowUserAppsOnly,
                    IncludeSystemApps = ShowSystemApps,
                    KeywordFilter = string.IsNullOrWhiteSpace(AppFilterKeyword) ? null : AppFilterKeyword,
                    WithSizes = false
                };

                var apps = await _appInventory.ListInstalledAppsAsync(SelectedDeviceSerial, options);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    InstalledApps.Clear();
                    
                    // Use HashSet for O(1) duplicate checking during UI updates
                    var addedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    
                    foreach (var app in apps)
                    {
                        // Skip duplicates at UI level as final safety check
                        if (!addedPackages.Contains(app.PackageName))
                        {
                            var appVM = new InstalledAppViewModel(app);
                            appVM.PropertyChanged += OnInstalledAppPropertyChanged;
                            InstalledApps.Add(appVM);
                            addedPackages.Add(app.PackageName);
                        }
                    }

                    ApplyInstalledAppsFilter();
                    RefreshInstalledAppsComputedProperties();
                });

                AppendLog($"✅ Loaded {apps.Count} installed apps");
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Failed to load installed apps: {ex.Message}");
            }
            finally
            {
                IsLoadingApps = false;
            }
        }

        [RelayCommand]
        private void SelectAllInstalledApps()
        {
            foreach (var app in FilteredInstalledApps)
            {
                app.IsSelected = true;
            }
            RefreshInstalledAppsComputedProperties();
        }

        [RelayCommand]
        private void ClearInstalledAppsSelection()
        {
            foreach (var app in FilteredInstalledApps)
            {
                app.IsSelected = false;
            }
            RefreshInstalledAppsComputedProperties();
        }

        private void ApplyInstalledAppsFilter()
        {
            var filtered = InstalledApps.AsEnumerable();

            // Apply app type filter
            if (ShowUserAppsOnly && !ShowSystemApps)
            {
                filtered = filtered.Where(app => !app.IsSystemApp);
            }
            else if (!ShowUserAppsOnly && ShowSystemApps)
            {
                filtered = filtered.Where(app => app.IsSystemApp);
            }

            // Apply keyword filter
            if (!string.IsNullOrWhiteSpace(AppFilterKeyword))
            {
                var keyword = AppFilterKeyword.Trim().ToLowerInvariant();
                filtered = filtered.Where(app =>
                    app.PackageName.ToLowerInvariant().Contains(keyword) ||
                    app.DisplayName.ToLowerInvariant().Contains(keyword));
            }

            // Update filtered collection
            FilteredInstalledApps.Clear();
            foreach (var app in filtered.OrderBy(a => a.DisplayName))
            {
                FilteredInstalledApps.Add(app);
            }
        }

        partial void OnShowUserAppsOnlyChanged(bool value)
        {
            ApplyInstalledAppsFilter();
            RefreshInstalledAppsComputedProperties();
        }

        partial void OnShowSystemAppsChanged(bool value)
        {
            ApplyInstalledAppsFilter();
            RefreshInstalledAppsComputedProperties();
        }

        partial void OnAppFilterKeywordChanged(string value)
        {
            ApplyInstalledAppsFilter();
            RefreshInstalledAppsComputedProperties();
        }

        [RelayCommand]
        private void ChooseExportDirectory()
        {
            var dlg = new VistaFolderBrowserDialog();
            dlg.ShowNewFolderButton = true;
            if (dlg.ShowDialog() == true)
            {
                ExportDirectory = dlg.SelectedPath;
                AppendLog($"Export directory set to: {ExportDirectory}");
            }
        }

        [RelayCommand]
        private async Task ExportSelectedApksAsync()
        {
            var selectedApps = InstalledApps.Where(a => a.IsSelected).ToList();
            if (selectedApps.Count == 0)
            {
                AppendLog("[INFO] No apps selected for export");
                return;
            }

            if (string.IsNullOrEmpty(ExportDirectory))
            {
                AppendLog("[INFO] Please select export directory first");
                return;
            }

            if (string.IsNullOrEmpty(SelectedDeviceSerial))
            {
                AppendLog("[INFO] No device selected");
                return;
            }

            try
            {
                AppendLog($"Starting export of {selectedApps.Count} app(s)...");

                var packageNames = selectedApps.Select(a => a.PackageName);
                var progress = new Progress<string>(AppendLog);

                var result = await _apkExporter.ExportMultipleApksAsync(
                    SelectedDeviceSerial,
                    packageNames,
                    ExportDirectory,
                    ExportIncludeSplits,
                    progress);

                if (result.Success)
                {
                    AppendLog($"✅ Export completed successfully! {result.ExportedPaths.Count} files exported");
                }
                else
                {
                    AppendLog($"❌ Export completed with errors: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Export failed: {ex.Message}");
            }
        }

        private void OnInstalledAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InstalledAppViewModel.IsSelected))
            {
                Application.Current.Dispatcher.BeginInvoke(RefreshInstalledAppsComputedProperties);
            }
        }

        private void RefreshInstalledAppsComputedProperties()
        {
            OnPropertyChanged(nameof(InstalledAppsCountText));
            OnPropertyChanged(nameof(SelectedInstalledAppsCount));
            OnPropertyChanged(nameof(SelectedInstalledAppsCountText));
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

        [RelayCommand]
        private async Task ValidateSelectedApksAsync()
        {
            var selectedApks = GetSelectedApks();
            if (selectedApks.Count == 0)
            {
                AppendLog("[INFO] No APK files selected for validation");
                return;
            }

            AppendLog($"🔍 Validating {selectedApks.Count} selected APK file(s)...");

            foreach (var apk in selectedApks)
            {
                try
                {
                    AppendLog($"\n📱 Validating: {Path.GetFileName(apk.FilePath)}");
                    
                    var validationResult = await _apkValidator.ValidateApkAsync(apk.FilePath);
                    
                    if (validationResult.IsValid)
                    {
                        AppendLog($"✅ Valid APK");
                        
                        if (validationResult.ApkInfo != null)
                        {
                            var info = validationResult.ApkInfo;
                            AppendLog($"   Package: {info.PackageName}");
                            AppendLog($"   Version: {info.VersionName} ({info.VersionCode})");
                            AppendLog($"   Min SDK: {info.MinSdkVersion}, Target SDK: {info.TargetSdkVersion}");
                            AppendLog($"   Size: {FormatFileSize(info.FileSize)}");
                            AppendLog($"   Has Splits: {info.HasSplits}");
                            
                            if (info.HasNativeLibraries)
                            {
                                AppendLog($"   Native Libraries: Yes");
                                if (info.SupportedArchitectures.Count > 0)
                                {
                                    AppendLog($"   Supported Architectures: {string.Join(", ", info.SupportedArchitectures)}");
                                }
                                else
                                {
                                    AppendLog($"   ⚠️ Architecture could not be determined");
                                }
                            }
                            else
                            {
                                AppendLog($"   Native Libraries: No (Universal APK)");
                            }
                            
                            if (info.Permissions.Count > 0)
                            {
                                AppendLog($"   Permissions: {info.Permissions.Count}");
                            }
                        }
                        
                        if (validationResult.Warnings.Any())
                        {
                            AppendLog($"⚠️ Warnings:");
                            foreach (var warning in validationResult.Warnings)
                            {
                                AppendLog($"   - {warning}");
                            }
                        }
                    }
                    else
                    {
                        AppendLog($"❌ Invalid APK: {validationResult.ErrorMessage}");
                        
                        if (validationResult.Warnings.Any())
                        {
                            AppendLog($"⚠️ Additional warnings:");
                            foreach (var warning in validationResult.Warnings)
                            {
                                AppendLog($"   - {warning}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"❌ Error validating {Path.GetFileName(apk.FilePath)}: {ex.Message}");
                }
            }
            
            AppendLog("\n🔍 APK validation completed");
        }

        [RelayCommand]
        private async Task RepairSelectedApksAsync()
        {
            var selectedApks = GetSelectedApks();
            if (selectedApks.Count == 0)
            {
                AppendLog("[INFO] No APK files selected for repair");
                return;
            }

            AppendLog($"🔧 Attempting to repair {selectedApks.Count} selected APK file(s)...");

            foreach (var apk in selectedApks)
            {
                try
                {
                    AppendLog($"\n🔧 Repairing: {Path.GetFileName(apk.FilePath)}");
                    
                    // First validate to see what's wrong
                    var validationResult = await _apkValidator.ValidateApkAsync(apk.FilePath);
                    
                    if (validationResult.IsValid)
                    {
                        AppendLog($"✅ APK is already valid, no repair needed");
                        continue;
                    }
                    
                    // Create repair path
                    var repairDir = Path.Combine(Path.GetDirectoryName(apk.FilePath)!, "repaired");
                    Directory.CreateDirectory(repairDir);
                    
                    var repairPath = Path.Combine(repairDir, $"repaired_{Path.GetFileName(apk.FilePath)}");
                    
                    // Attempt repair
                    var repairSuccess = await _apkValidator.TryRepairApkAsync(apk.FilePath, repairPath);
                    
                    if (repairSuccess)
                    {
                        AppendLog($"✅ Repair completed: {Path.GetFileName(repairPath)}");
                        
                        // Validate the repaired APK
                        var repairedValidation = await _apkValidator.ValidateApkAsync(repairPath);
                        if (repairedValidation.IsValid)
                        {
                            AppendLog($"✅ Repaired APK is now valid");
                        }
                        else
                        {
                            AppendLog($"⚠️ Repaired APK still has issues: {repairedValidation.ErrorMessage}");
                        }
                    }
                    else
                    {
                        AppendLog($"❌ Failed to repair APK");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"❌ Error repairing {Path.GetFileName(apk.FilePath)}: {ex.Message}");
                }
            }
            
            AppendLog("\n🔧 APK repair completed");
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }

        [RelayCommand]
        private async Task CheckArchitectureCompatibilityAsync()
        {
            var selectedApks = GetSelectedApks();
            var selectedDevices = Devices.Where(d => d.IsSelected).ToList();
            
            if (selectedApks.Count == 0)
            {
                AppendLog("[INFO] No APK files selected for architecture compatibility check");
                return;
            }

            if (selectedDevices.Count == 0)
            {
                AppendLog("[INFO] No devices selected for architecture compatibility check");
                return;
            }

            AppendLog($"🔍 Checking architecture compatibility for {selectedApks.Count} APK(s) and {selectedDevices.Count} device(s)...");

            foreach (var device in selectedDevices)
            {
                AppendLog($"\n📱 Device: {device.DisplayName}");
                AppendLog($"   Architecture: {device.Model.Abi ?? "Unknown"}");
                AppendLog($"   Android: {device.Model.AndroidVersion} (API {device.Model.Sdk})");
                
                foreach (var apk in selectedApks)
                {
                    try
                    {
                        AppendLog($"\n   📦 APK: {Path.GetFileName(apk.FilePath)}");
                        
                        var validationResult = await _apkValidator.ValidateApkAsync(apk.FilePath);
                        
                        if (validationResult.IsValid && validationResult.ApkInfo != null)
                        {
                            var info = validationResult.ApkInfo;
                            
                            if (info.HasNativeLibraries)
                            {
                                if (info.SupportedArchitectures.Count > 0)
                                {
                                    var deviceArch = device.Model.Abi?.ToLower();
                                    var isCompatible = false;
                                    
                                    if (!string.IsNullOrEmpty(deviceArch))
                                    {
                                        // Check for architecture compatibility
                                        isCompatible = CheckArchitectureCompatibility(deviceArch, info.SupportedArchitectures);
                                    }
                                    
                                    if (isCompatible)
                                    {
                                        AppendLog($"      ✅ Compatible: Device {deviceArch} is supported");
                                    }
                                    else
                                    {
                                        AppendLog($"      ❌ Incompatible: Device {deviceArch} not supported");
                                        AppendLog($"         APK supports: {string.Join(", ", info.SupportedArchitectures)}");
                                        AppendLog($"         💡 Solution: Find APK for {deviceArch} architecture or use universal APK");
                                    }
                                }
                                else
                                {
                                    AppendLog($"      ⚠️ Warning: Architecture could not be determined");
                                }
                            }
                            else
                            {
                                AppendLog($"      ✅ Universal APK: No architecture-specific libraries");
                            }
                        }
                        else
                        {
                            AppendLog($"      ❌ APK validation failed: {validationResult.ErrorMessage}");
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"      ❌ Error checking compatibility: {ex.Message}");
                    }
                }
            }
            
            AppendLog("\n🔍 Architecture compatibility check completed");
        }

        private static bool CheckArchitectureCompatibility(string deviceArch, List<string> supportedArchs)
        {
            // Map device architectures to APK architectures
            var archMapping = new Dictionary<string, string[]>
            {
                ["arm64-v8a"] = new[] { "arm64-v8a" },
                ["armeabi-v7a"] = new[] { "armeabi-v7a", "armeabi" },
                ["armeabi"] = new[] { "armeabi" },
                ["x86_64"] = new[] { "x86_64", "x86" },
                ["x86"] = new[] { "x86" },
                ["mips64"] = new[] { "mips64", "mips" },
                ["mips"] = new[] { "mips" }
            };

            if (archMapping.TryGetValue(deviceArch, out var compatibleArchs))
            {
                return supportedArchs.Any(apkArch => compatibleArchs.Contains(apkArch));
            }

            return false;
        }

    }
}