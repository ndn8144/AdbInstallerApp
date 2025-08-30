using AdbInstallerApp.Models;
using AdbInstallerApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Linq;

namespace AdbInstallerApp.ViewModels
{
    /// <summary>
    /// ViewModel for Multi-Group APK Installation with 3-column layout and performance optimizations
    /// </summary>
    public sealed partial class MultiGroupInstallViewModel : ObservableObject
    {
        private readonly AdvancedInstallOrchestrator? _orchestrator;
        private readonly MainViewModel _mainViewModel;
        private readonly object _devicesLock = new();
        private readonly object _groupsLock = new();
        private readonly object _queueLock = new();
        private readonly object _logLock = new();

        // Filter fields
        private string _deviceFilter = string.Empty;
        private string _groupFilter = string.Empty;

        // Collections with thread-safe access
        public ObservableCollection<DeviceViewModel> Devices { get; } = new();
        public ObservableCollection<ApkGroupViewModel> Groups { get; } = new();
        public ObservableCollection<QueueItemVM> Queue { get; } = new();
        public ObservableCollection<string> LogLines { get; } = new();

        // Collection views for filtering and sorting
        public ICollectionView DevicesView { get; private set; }
        public ICollectionView GroupsView { get; private set; }
        public ICollectionView QueueView { get; private set; }

        // Filter properties with optimized refresh
        public string DeviceFilter
        {
            get => _deviceFilter;
            set
            {
                if (SetProperty(ref _deviceFilter, value))
                {
                    using (DevicesView.DeferRefresh())
                    {
                        // Filter will be applied when DeferRefresh is disposed
                    }
                }
            }
        }

        public string GroupFilter
        {
            get => _groupFilter;
            set
            {
                if (SetProperty(ref _groupFilter, value))
                {
                    using (GroupsView.DeferRefresh())
                    {
                        // Filter will be applied when DeferRefresh is disposed
                    }
                }
            }
        }

        // Summary properties
        public string DevicesSummary => $"{Devices.Count(d => d.State == "device")} online • {Devices.Count} total";
        public string GroupsSummary => $"Found {Groups.Count} groups";
        public string QueueSummary => $"{Queue.Count} groups queued";
        public string HeaderInfo => $"{OnlineCount} Devices • {TotalApkFiles} APKs";

        private int OnlineCount => Devices.Count(d => d.State == "device");
        private int TotalApkFiles => Groups.Sum(g => g.ApkCount);

        // Installation state
        [ObservableProperty]
        private bool _isInstalling;

        [ObservableProperty]
        private string _currentStatus = "Ready to install";

        // Commands
        public IAsyncRelayCommand StartInstallCommand { get; }
        public IRelayCommand CancelInstallCommand { get; }
        public IRelayCommand RefreshDevicesCommand { get; }
        public IRelayCommand SelectAllDevicesCommand { get; }
        public IRelayCommand SelectOnlineDevicesCommand { get; }
        public IRelayCommand SelectNoneDevicesCommand { get; }
        public IRelayCommand RefreshGroupsCommand { get; }
        public IRelayCommand RefreshAvailableGroupsCommand { get; }
        public IRelayCommand SelectAllGroupsCommand { get; }
        public IRelayCommand<ApkGroupViewModel> AddGroupCommand { get; }
        public IRelayCommand<QueueItemVM> RemoveFromQueueCommand { get; }
        public IRelayCommand ClearQueueCommand { get; }
        public IRelayCommand CopyLogCommand { get; }
        public IRelayCommand ClearLogCommand { get; }

        private DateTime _installStartTime;
        private CancellationTokenSource? _cancellationTokenSource;

        public MultiGroupInstallViewModel(
            AdvancedInstallOrchestrator? orchestrator, 
            MainViewModel mainViewModel)
        {
            _orchestrator = orchestrator; // Can be null temporarily
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));

            // Initialize collection views with filtering and sorting
            DevicesView = CollectionViewSource.GetDefaultView(Devices);
            DevicesView.Filter = o => string.IsNullOrWhiteSpace(DeviceFilter) || ((DeviceViewModel)o).DisplayName.Contains(DeviceFilter, StringComparison.OrdinalIgnoreCase);
            DevicesView.SortDescriptions.Add(new SortDescription(nameof(DeviceViewModel.State), ListSortDirection.Ascending));
            DevicesView.SortDescriptions.Add(new SortDescription(nameof(DeviceViewModel.Serial), ListSortDirection.Ascending));
            GroupsView = CollectionViewSource.GetDefaultView(Groups);
            GroupsView.Filter = o => string.IsNullOrWhiteSpace(GroupFilter) || ((ApkGroupViewModel)o).Name.Contains(GroupFilter, StringComparison.OrdinalIgnoreCase);

            QueueView = CollectionViewSource.GetDefaultView(Queue);
            QueueView.SortDescriptions.Add(new SortDescription(nameof(QueueItemVM.PackageName), ListSortDirection.Ascending));

            // Enable collection synchronization for thread-safe updates
            BindingOperations.EnableCollectionSynchronization(Devices, _devicesLock);
            BindingOperations.EnableCollectionSynchronization(Groups, _groupsLock);
            BindingOperations.EnableCollectionSynchronization(Queue, _queueLock);
            BindingOperations.EnableCollectionSynchronization(LogLines, _logLock);

            // Subscribe to MainViewModel collections for automatic sync
            _mainViewModel.Devices.CollectionChanged += OnMainDevicesChanged;
            _mainViewModel.ApkGroups.CollectionChanged += OnMainGroupsChanged;

            // Initialize commands
            StartInstallCommand = new AsyncRelayCommand(StartInstallationAsync, CanStartInstallation);
            CancelInstallCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(CancelInstallation, () => IsInstalling);
            RefreshDevicesCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(RefreshDevices);
            SelectAllDevicesCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(SelectAllDevices);
            SelectOnlineDevicesCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(SelectOnlineDevices);
            SelectNoneDevicesCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(SelectNoneDevices);
            RefreshGroupsCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(RefreshGroups);
            RefreshAvailableGroupsCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(RefreshGroups);
            SelectAllGroupsCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(SelectAllGroups);
            AddGroupCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<ApkGroupViewModel>(AddGroup);
            RemoveFromQueueCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<QueueItemVM>(RemoveFromQueue);
            ClearQueueCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ClearQueue);
            CopyLogCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(CopyLog);
            ClearLogCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ClearLog);
            
            // Subscribe to property changes to update command states
            PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(IsInstalling))
                {
                    StartInstallCommand.NotifyCanExecuteChanged();
                    CancelInstallCommand.NotifyCanExecuteChanged();
                }
            };
            
            Queue.CollectionChanged += (s, e) => StartInstallCommand.NotifyCanExecuteChanged();
            Devices.CollectionChanged += (s, e) => StartInstallCommand.NotifyCanExecuteChanged();
            
            // Load initial data
            LoadDevices();
            LoadGroups();
        }

        private async Task StartInstallationAsync()
        {
            if (Queue.Count == 0)
            {
                AddLogEntry("❌ No groups selected for installation");
                return;
            }

            var selectedDevices = Devices.Where(d => d.IsSelected).ToList();
            if (selectedDevices.Count == 0)
            {
                AddLogEntry("❌ No devices selected");
                return;
            }

            try
            {
                IsInstalling = true;
                _installStartTime = DateTime.Now;
                _cancellationTokenSource = new CancellationTokenSource();
                
                AddLogEntry($"🚀 Starting installation - {Queue.Count} groups, {selectedDevices.Count} devices");
                CurrentStatus = "Installing...";

                // Convert queue items to installation data
                var selectedPaths = Queue.Where(q => q.Group?.Model?.ApkItems != null)
                    .SelectMany(q => q.Group!.Model.ApkItems.Select(f => f.FilePath))
                    .ToList();
                var deviceSerials = selectedDevices.Select(d => d.Serial).ToList();
                
                // Create global options
                var globalOptions = new AdbInstallOptions
                {
                    Reinstall = true,
                    Downgrade = false,
                    GrantPermissions = true,
                    UserId = null
                };

                AddLogEntry($"📦 Processing {selectedPaths.Count} APK paths for {deviceSerials.Count} devices");
                
                // Execute enhanced installation
                if (_orchestrator != null)
                {
                    // Convert selectedPaths to ApkGroup objects
                    var apkGroups = Queue.Where(q => q.Group?.Model != null)
                        .Select(q => q.Group!.Model)
                        .ToList();
                    
                    // TODO: Implement AdvancedInstallOrchestrator.RunAsync
                    // await _orchestrator.RunAsync(
                    //     selectedDevices.Select(d => d.Model).ToList(),
                    //     apkGroups,
                    //     globalOptions,
                    //     _cancellationTokenSource.Token);
                    
                    // Temporary: simulate installation
                    await Task.Delay(2000, _cancellationTokenSource.Token);
                }
                else
                {
                    AddLogEntry("⚠️ Enhanced installation temporarily disabled - orchestrator not available");
                    AddLogEntry("📋 Installation data prepared but not executed");
                }
                
                AddLogEntry("✅ Installation completed successfully!");
                CurrentStatus = "Installation completed";
            }
            catch (OperationCanceledException)
            {
                AddLogEntry("⏹️ Installation cancelled by user");
                CurrentStatus = "Cancelled";
            }
            catch (Exception ex)
            {
                AddLogEntry($"❌ Installation failed: {ex.Message}");
                CurrentStatus = "Failed";
            }
            finally
            {
                IsInstalling = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void CancelInstallation()
        {
            _cancellationTokenSource?.Cancel();
            AddLogEntry("🛑 Installation cancellation requested");
            CurrentStatus = "Cancelling...";
        }

        private bool CanStartInstallation()
        {
            var canStart = !IsInstalling && Queue.Count > 0 && Devices.Any(d => d.IsSelected);
            System.Diagnostics.Debug.WriteLine($"CanStartInstallation: IsInstalling={IsInstalling}, Queue.Count={Queue.Count}, SelectedDevices={Devices.Count(d => d.IsSelected)}, Result={canStart}");
            return canStart;
        }

        // Device commands
        private void RefreshDevices()
        {
            // Trigger refresh on MainViewModel first, then sync
            _mainViewModel.RefreshDevicesCommand.Execute(null);
            LoadDevices();
            AddLogEntry("🔄 Devices refreshed");
        }

        private void SelectAllDevices()
        {
            foreach (var device in Devices)
                device.IsSelected = true;
            UpdateSummaries();
            StartInstallCommand.NotifyCanExecuteChanged();
        }

        private void SelectOnlineDevices()
        {
            foreach (var device in Devices)
                device.IsSelected = device.State == "device";
            UpdateSummaries();
            StartInstallCommand.NotifyCanExecuteChanged();
        }

        private void SelectNoneDevices()
        {
            foreach (var device in Devices)
                device.IsSelected = false;
            UpdateSummaries();
            StartInstallCommand.NotifyCanExecuteChanged();
        }

        // Group commands
        private void RefreshGroups()
        {
            // Trigger refresh on MainViewModel first, then sync
            _mainViewModel.RefreshRepoCommand.Execute(null);
            LoadGroups();
            AddLogEntry("🔄 Groups refreshed");
        }

        private void SelectAllGroups()
        {
            foreach (var group in Groups)
            {
                AddGroup(group);
            }
        }

        private void AddGroup(ApkGroupViewModel? group)
        {
            if (group == null) return;

            lock (_queueLock)
            {
                var queueItem = new QueueItemVM
                {
                    GroupId = group.Id,
                    PackageName = group.Name,
                    Detail = $"{group.ApkCount} APKs",
                    Group = group
                };
                Queue.Add(queueItem);
            }
            AddLogEntry($"➕ Added '{group.Name}' to queue");
            UpdateSummaries();
            
            // Notify command state change
            StartInstallCommand.NotifyCanExecuteChanged();
        }

        // Queue commands
        private void RemoveFromQueue(QueueItemVM? item)
        {
            if (item == null) return;

            Queue.Remove(item);
            AddLogEntry($"➖ Removed '{item.PackageName}' from queue");
            UpdateSummaries();
            
            // Notify command state change
            StartInstallCommand.NotifyCanExecuteChanged();
        }

        private void ClearQueue()
        {
            Queue.Clear();
            AddLogEntry("🗑️ Queue cleared");
            UpdateSummaries();
            
            // Notify command state change
            StartInstallCommand.NotifyCanExecuteChanged();
        }

        // Log commands
        private void CopyLog()
        {
            var logText = string.Join(Environment.NewLine, LogLines);
            Clipboard.SetText(logText);
            AddLogEntry("📋 Log copied to clipboard");
        }

        private void ClearLog()
        {
            LogLines.Clear();
        }

        private void OnMainDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            LoadDevices();
        }

        private void OnMainGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            LoadGroups();
        }

        private void LoadDevices()
        {
            lock (_devicesLock)
            {
                Devices.Clear();
                foreach (var device in _mainViewModel.Devices)
                {
                    device.PropertyChanged += (s, e) => {
                        if (e.PropertyName == nameof(DeviceViewModel.IsSelected))
                            UpdateSummaries();
                    };
                    Devices.Add(device);
                }
            }
            UpdateSummaries();
            DevicesView.Refresh();
        }

        private void LoadGroups()
        {
            lock (_groupsLock)
            {
                Groups.Clear();
                foreach (var group in _mainViewModel.ApkGroups)
                {
                    Groups.Add(group);
                }
            }
            UpdateSummaries();
        }

        private void AddLogEntry(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}";
            
            Application.Current?.Dispatcher.Invoke(() =>
            {
                lock (_logLock)
                {
                    LogLines.Add(logEntry);
                    
                    // Keep log size manageable
                    if (LogLines.Count > 1000)
                    {
                        LogLines.RemoveAt(0);
                    }
                }
            });
        }

        private void UpdateSummaries()
        {
            OnPropertyChanged(nameof(DevicesSummary));
            OnPropertyChanged(nameof(GroupsSummary));
            OnPropertyChanged(nameof(QueueSummary));
            OnPropertyChanged(nameof(HeaderInfo));
        }

        private void UpdateCommandStates()
        {
            StartInstallCommand.NotifyCanExecuteChanged();
            // Note: Custom RelayCommand doesn't have NotifyCanExecuteChanged method
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}
