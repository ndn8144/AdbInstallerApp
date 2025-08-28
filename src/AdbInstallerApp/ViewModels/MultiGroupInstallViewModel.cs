using AdbInstallerApp.Models;
using AdbInstallerApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace AdbInstallerApp.ViewModels
{
    /// <summary>
    /// ViewModel cho UI multi-group installation với real-time progress tracking
    /// </summary>
    public partial class MultiGroupInstallViewModel : ObservableObject
    {
        private readonly MultiGroupInstallOrchestrator _orchestrator;
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty]
        private bool _isInstalling;

        partial void OnIsInstallingChanged(bool value)
        {
            _mainViewModel.AppendLog($"🔄 MultiGroup: IsInstalling changed to {value}");
            
            // Notify all commands that depend on IsInstalling state
            StartInstallationCommand.NotifyCanExecuteChanged();
            CancelInstallationCommand.NotifyCanExecuteChanged();
        }

        [ObservableProperty]
        private double _overallProgress;

        partial void OnOverallProgressChanged(double value)
        {
            OnPropertyChanged(nameof(ComputedProgressText));
            ProgressText = $"{value:F1}%";
        }

        [ObservableProperty]
        private string _currentStatus = "Ready to install";

        [ObservableProperty]
        private string _currentGroup = "";

        [ObservableProperty]
        private string _currentApk = "";

        [ObservableProperty]
        private string _progressText = "0%";
        
        // Computed property for progress text
        public string ComputedProgressText => $"{OverallProgress:F1}%";

        [ObservableProperty]
        private string _estimatedTimeRemaining = "";

        // Collections cho UI binding
        public ObservableCollection<GroupInstallTaskViewModel> AvailableGroups { get; } = new();
        public ObservableCollection<GroupInstallTaskViewModel> SelectedGroups { get; } = new();
        public ObservableCollection<InstallProgressItemViewModel> ProgressItems { get; } = new();

        // Installation options
        [ObservableProperty]
        private bool _reinstallEnabled = true;

        [ObservableProperty]
        private bool _grantPermissions = false;

        [ObservableProperty]
        private bool _allowDowngrade = false;

        // Commands
        public IAsyncRelayCommand StartInstallationCommand { get; }
        public IRelayCommand CancelInstallationCommand { get; }
        public IRelayCommand AddGroupToQueueCommand { get; }
        public IRelayCommand RemoveGroupFromQueueCommand { get; }
        public IRelayCommand ClearQueueCommand { get; }
        public IRelayCommand SelectAllGroupsCommand { get; }
        public IRelayCommand RefreshAvailableGroupsCommand { get; }

        private DateTime _installStartTime;
        private CancellationTokenSource? _cancellationTokenSource;

        public MultiGroupInstallViewModel(
            MultiGroupInstallOrchestrator orchestrator, 
            MainViewModel mainViewModel)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));

            // Initialize commands
            StartInstallationCommand = new AsyncRelayCommand(StartInstallationAsync, CanStartInstallation);
            CancelInstallationCommand = new RelayCommand(CancelInstallation, () => IsInstalling);
            AddGroupToQueueCommand = new RelayCommand<GroupInstallTaskViewModel>(AddGroupToQueue);
            RemoveGroupFromQueueCommand = new RelayCommand<GroupInstallTaskViewModel>(RemoveGroupFromQueue);
            ClearQueueCommand = new RelayCommand(ClearQueue, () => SelectedGroups.Count > 0);
            SelectAllGroupsCommand = new RelayCommand(SelectAllGroups);
            RefreshAvailableGroupsCommand = new RelayCommand(RefreshAvailableGroups);
            
            // Debug logging for command initialization
            _mainViewModel.AppendLog("🔧 MultiGroup: Commands initialized");

            // Subscribe to orchestrator events
            _orchestrator.PropertyChanged += OnOrchestratorPropertyChanged;

            // Load available groups
            LoadAvailableGroups();
        }

        /// <summary>
        /// Bắt đầu quá trình cài đặt multi-group
        /// </summary>
        private async Task StartInstallationAsync()
        {
            _mainViewModel.AppendLog("🚀 MultiGroup: StartInstallationAsync called");
            
            if (SelectedGroups.Count == 0)
            {
                CurrentStatus = "❌ No groups selected for installation";
                _mainViewModel.AppendLog("❌ MultiGroup: No groups selected for installation");
                return;
            }

            var selectedDevices = _mainViewModel.Devices.Where(d => d.IsSelected).Select(d => d.Model).ToList();
            if (selectedDevices.Count == 0)
            {
                CurrentStatus = "❌ No devices selected";
                _mainViewModel.AppendLog("❌ MultiGroup: No devices selected");
                return;
            }

            _mainViewModel.AppendLog($"📋 MultiGroup: Starting installation - {SelectedGroups.Count} groups, {selectedDevices.Count} devices");

            try
            {
                IsInstalling = true;
                _installStartTime = DateTime.Now;
                _cancellationTokenSource = new CancellationTokenSource();
                
                // Clear previous progress
                ProgressItems.Clear();
                
                // Tạo install tasks từ selected groups
                var installTasks = SelectedGroups.Select(group => new GroupInstallTask(
                    group.GroupName,
                    selectedDevices,
                    group.ApkFiles.ToList(),
                    new InstallOptions(ReinstallEnabled, GrantPermissions, AllowDowngrade)
                )).ToList();

                // Tạo progress items cho UI tracking
                foreach (var group in SelectedGroups)
                {
                    ProgressItems.Add(new InstallProgressItemViewModel(group.GroupName, group.ApkFiles.Count));
                }

                CurrentStatus = $"🚀 Starting installation of {installTasks.Count} groups...";

                // Use the new InstallOrchestrator with centralized progress
                _mainViewModel.AppendLog("🔄 MultiGroup: Using InstallOrchestrator.InstallGroupsAsync");
                
                // Convert to required format
                var devices = selectedDevices;
                var groups = SelectedGroups.Select(sg => new ApkGroup 
                { 
                    Name = sg.GroupName, 
                    ApkItems = new ObservableCollection<ApkItem>(sg.ApkFiles)
                }).ToList();
                
                await _mainViewModel._installer.InstallGroupsAsync(
                    devices,
                    groups,
                    ReinstallEnabled,
                    GrantPermissions,
                    AllowDowngrade,
                    _mainViewModel.AppendLog,
                    _cancellationTokenSource.Token);

                // Installation completed successfully
                CurrentStatus = "✅ Installation completed successfully!";
                _mainViewModel.AppendLog("✅ MultiGroup: Installation completed successfully!");
            }
            catch (OperationCanceledException)
            {
                CurrentStatus = "⏹️ Installation cancelled by user";
            }
            catch (Exception ex)
            {
                CurrentStatus = $"❌ Installation failed: {ex.Message}";
            }
            finally
            {
                IsInstalling = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _mainViewModel.AppendLog("🔄 MultiGroup: Installation process finished, IsInstalling = false");
            }
        }

        /// <summary>
        /// Xử lý progress updates từ orchestrator
        /// </summary>
        private void OnInstallProgress(InstallProgressInfo progressInfo)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                OverallProgress = progressInfo.Progress;
                CurrentStatus = progressInfo.Message;
                CurrentGroup = progressInfo.CurrentGroup;
                CurrentApk = progressInfo.CurrentApk;
                ProgressText = $"{progressInfo.Progress:F1}%";

                // Update estimated time remaining
                UpdateEstimatedTimeRemaining(progressInfo.Progress);

                // Update specific group progress
                var progressItem = ProgressItems.FirstOrDefault(p => p.GroupName == progressInfo.CurrentGroup);
                if (progressItem != null)
                {
                    progressItem.UpdateProgress(progressInfo);
                }

                // Log progress to main view
                _mainViewModel.AppendLog($"[INSTALL] {progressInfo.Message}");
            });
        }

        /// <summary>
        /// Tính toán thời gian còn lại dựa trên progress hiện tại
        /// </summary>
        private void UpdateEstimatedTimeRemaining(double currentProgress)
        {
            if (currentProgress <= 0 || !IsInstalling)
            {
                EstimatedTimeRemaining = "";
                return;
            }

            var elapsed = DateTime.Now - _installStartTime;
            var estimatedTotal = TimeSpan.FromMilliseconds(elapsed.TotalMilliseconds * (100.0 / currentProgress));
            var remaining = estimatedTotal - elapsed;

            if (remaining.TotalSeconds > 0)
            {
                EstimatedTimeRemaining = remaining.TotalMinutes >= 1 
                    ? $"~{remaining.Minutes}m {remaining.Seconds}s remaining"
                    : $"~{remaining.Seconds}s remaining";
            }
            else
            {
                EstimatedTimeRemaining = "Almost done...";
            }
        }

        /// <summary>
        /// Xử lý kết quả installation
        /// </summary>
        private async Task HandleInstallationResult(MultiGroupInstallResult result)
        {
            var successCount = result.GroupResults.Count(r => r.Success);
            var totalCount = result.GroupResults.Count;

            if (result.Success)
            {
                CurrentStatus = $"✅ All {totalCount} groups installed successfully!";
                _mainViewModel.AppendLog($"🎉 Multi-group installation completed: {successCount}/{totalCount} groups successful");
            }
            else
            {
                CurrentStatus = $"⚠️ Installation completed with issues: {successCount}/{totalCount} groups successful";
                _mainViewModel.AppendLog($"⚠️ Multi-group installation completed with errors: {successCount}/{totalCount} groups successful");
            }

            // Update progress items với final status
            foreach (var groupResult in result.GroupResults)
            {
                var progressItem = ProgressItems.FirstOrDefault(p => p.GroupName == groupResult.GroupName);
                progressItem?.SetFinalStatus(groupResult.Success, groupResult.Message);

                // Log detailed results
                _mainViewModel.AppendLog($"📋 Group '{groupResult.GroupName}': {groupResult.Message}");
                
                foreach (var apkResult in groupResult.ApkResults)
                {
                    var status = apkResult.Success ? "✅" : "❌";
                    _mainViewModel.AppendLog($"  {status} {Path.GetFileName(apkResult.ApkPath)}: {apkResult.Message}");
                }
            }

            // Auto-clear queue nếu tất cả thành công
            if (result.Success)
            {
                await Task.Delay(2000); // Show success message for 2 seconds
                ClearQueue();
            }
        }

        /// <summary>
        /// Load danh sách groups có sẵn từ MainViewModel
        /// </summary>
        private void LoadAvailableGroups()
        {
            AvailableGroups.Clear();
            
            // Debug logging
            _mainViewModel.AppendLog($"🔍 MultiGroup: Loading groups from MainViewModel ({_mainViewModel.ApkGroups.Count} groups available)");
            
            foreach (var group in _mainViewModel.ApkGroups)
            {
                if (group.ApkItems.Count > 0) // Chỉ show groups có APK
                {
                    var groupViewModel = new GroupInstallTaskViewModel(
                        group.Name,
                        group.Description ?? "",
                        group.ApkItems.Select(apk => apk.Model).ToList());
                    
                    AvailableGroups.Add(groupViewModel);
                    _mainViewModel.AppendLog($"  ✅ Added group: {group.Name} ({group.ApkItems.Count} APKs)");
                }
                else
                {
                    _mainViewModel.AppendLog($"  ⚠️ Skipped empty group: {group.Name}");
                }
            }
            
            _mainViewModel.AppendLog($"📦 MultiGroup: Loaded {AvailableGroups.Count} available groups");
            
            // Force UI update
            OnPropertyChanged(nameof(AvailableGroups));
        }

        /// <summary>
        /// Thêm group vào queue cài đặt
        /// </summary>
        private void AddGroupToQueue(GroupInstallTaskViewModel? group)
        {
            if (group == null || SelectedGroups.Contains(group)) return;

            SelectedGroups.Add(group);
            group.IsSelected = true;
            
            CurrentStatus = $"Added '{group.GroupName}' to installation queue";
            _mainViewModel.AppendLog($"➕ MultiGroup: Added '{group.GroupName}' to queue ({group.ApkCount} APKs)");
            
            // Update command states
            StartInstallationCommand.NotifyCanExecuteChanged();
            CancelInstallationCommand.NotifyCanExecuteChanged();
            ClearQueueCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Xóa group khỏi queue cài đặt
        /// </summary>
        private void RemoveGroupFromQueue(GroupInstallTaskViewModel? group)
        {
            if (group == null) return;

            SelectedGroups.Remove(group);
            group.IsSelected = false;
            
            CurrentStatus = $"Removed '{group.GroupName}' from installation queue";
            
            // Update command states
            StartInstallationCommand.NotifyCanExecuteChanged();
            ClearQueueCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Xóa tất cả groups khỏi queue
        /// </summary>
        private void ClearQueue()
        {
            foreach (var group in SelectedGroups.ToList())
            {
                group.IsSelected = false;
            }
            
            SelectedGroups.Clear();
            ProgressItems.Clear();
            
            CurrentStatus = "Installation queue cleared";
            
            // Update command states
            StartInstallationCommand.NotifyCanExecuteChanged();
            ClearQueueCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Chọn tất cả groups có sẵn
        /// </summary>
        private void SelectAllGroups()
        {
            foreach (var group in AvailableGroups)
            {
                if (!SelectedGroups.Contains(group))
                {
                    AddGroupToQueue(group);
                }
            }
        }

        /// <summary>
        /// Hủy bỏ installation đang chạy
        /// </summary>
        private void CancelInstallation()
        {
            _mainViewModel.AppendLog("🛑 MultiGroup: CancelInstallation called");
            _cancellationTokenSource?.Cancel();
            _orchestrator.CancelInstallation();
            CurrentStatus = "🛑 Cancelling installation...";
            _mainViewModel.AppendLog("🛑 MultiGroup: Installation cancellation requested");
        }

        /// <summary>
        /// Check xem có thể bắt đầu installation không
        /// </summary>
        private bool CanStartInstallation()
        {
            var canStart = !IsInstalling && 
                          SelectedGroups.Count > 0 && 
                          _mainViewModel.Devices.Any(d => d.IsSelected);
            
            // Debug logging
            if (!canStart)
            {
                var reason = IsInstalling ? "Already installing" : 
                            SelectedGroups.Count == 0 ? "No groups selected" : 
                            "No devices selected";
                _mainViewModel.AppendLog($"🔍 MultiGroup: CanStartInstallation = false ({reason})");
            }
            
            return canStart;
        }

        /// <summary>
        /// Handle property changes từ orchestrator
        /// </summary>
        private void OnOrchestratorPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(MultiGroupInstallOrchestrator.OverallProgress):
                        OverallProgress = _orchestrator.OverallProgress;
                        break;
                    case nameof(MultiGroupInstallOrchestrator.CurrentStatus):
                        CurrentStatus = _orchestrator.CurrentStatus;
                        break;
                    case nameof(MultiGroupInstallOrchestrator.CurrentGroup):
                        CurrentGroup = _orchestrator.CurrentGroup;
                        break;
                    case nameof(MultiGroupInstallOrchestrator.CurrentApk):
                        CurrentApk = _orchestrator.CurrentApk;
                        break;
                    case nameof(MultiGroupInstallOrchestrator.IsInstalling):
                        IsInstalling = _orchestrator.IsInstalling;
                        break;
                }
            });
        }

        /// <summary>
        /// Refresh available groups khi có thay đổi
        /// </summary>
        public void RefreshAvailableGroups()
        {
            _mainViewModel.AppendLog("🔄 MultiGroup: RefreshAvailableGroups called");
            LoadAvailableGroups();
            CurrentStatus = $"Refreshed: Found {AvailableGroups.Count} available groups";
            _mainViewModel.AppendLog($"🔄 MultiGroup: Refresh completed - {AvailableGroups.Count} groups found");
        }
    }

    #region Supporting ViewModels

    /// <summary>
    /// ViewModel cho một group trong danh sách
    /// </summary>
    public partial class GroupInstallTaskViewModel : ObservableObject
    {
        public string GroupName { get; }
        public string Description { get; }
        public List<ApkItem> ApkFiles { get; }
        public int ApkCount => ApkFiles.Count;
        public string ApkCountText => $"{ApkCount} APK{(ApkCount != 1 ? "s" : "")}";

        [ObservableProperty]
        private bool _isSelected;

        public GroupInstallTaskViewModel(string groupName, string description, List<ApkItem> apkFiles)
        {
            GroupName = groupName;
            Description = description;
            ApkFiles = apkFiles;
        }
    }

    /// <summary>
    /// ViewModel cho progress tracking của một group
    /// </summary>
    public partial class InstallProgressItemViewModel : ObservableObject
    {
        public string GroupName { get; }
        public int TotalApks { get; }

        [ObservableProperty]
        private double _progress;

        [ObservableProperty]
        private string _status = "Pending";

        [ObservableProperty]
        private string _currentApk = "";

        [ObservableProperty]
        private int _completedApks;

        [ObservableProperty]
        private bool _isCompleted;

        [ObservableProperty]
        private bool _hasError;

        public string ProgressText => $"{CompletedApks}/{TotalApks} APKs";

        public InstallProgressItemViewModel(string groupName, int totalApks)
        {
            GroupName = groupName;
            TotalApks = totalApks;
        }

        public void UpdateProgress(InstallProgressInfo progressInfo)
        {
            if (progressInfo.CurrentGroup != GroupName) return;

            Progress = progressInfo.Progress;
            Status = progressInfo.Message;
            CurrentApk = progressInfo.CurrentApk;
            
            // Estimate completed APKs based on progress
            CompletedApks = (int)Math.Floor(Progress / 100.0 * TotalApks);
            
            OnPropertyChanged(nameof(ProgressText));
        }

        public void SetFinalStatus(bool success, string message)
        {
            IsCompleted = true;
            HasError = !success;
            Status = message;
            Progress = 100;
            CompletedApks = success ? TotalApks : CompletedApks;
            
            OnPropertyChanged(nameof(ProgressText));
        }
    }

    #endregion
}
