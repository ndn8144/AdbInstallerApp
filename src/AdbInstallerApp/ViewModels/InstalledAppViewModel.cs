using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AdbInstallerApp.Models;
using AdbInstallerApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ookii.Dialogs.Wpf;

namespace AdbInstallerApp.ViewModels
{
    /// <summary>
    /// ViewModel for the Installed Apps module
    /// Handles app listing, filtering, selection, and APK export functionality
    /// </summary>
    public partial class InstalledAppViewModel : ObservableObject
    {
        private readonly AppInventoryService _appInventoryService = null!;
        private readonly ApkExportService _apkExportService = null!;
        private readonly AdbService _adbService = null!;
        
        private CancellationTokenSource? _loadCancellationTokenSource;
        private CancellationTokenSource? _exportCancellationTokenSource;

        [ObservableProperty]
        private ObservableCollection<InstalledApp> _installedApps = new();

        [ObservableProperty]
        private ObservableCollection<InstalledApp> _filteredApps = new();

        [ObservableProperty]
        private string _searchKeyword = string.Empty;

        [ObservableProperty]
        private AppFilterType _selectedFilterType = AppFilterType.All;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isExporting;

        [ObservableProperty]
        private bool _includeSplits = true;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private string _exportLog = string.Empty;

        [ObservableProperty]
        private int _totalAppsCount;

        [ObservableProperty]
        private int _selectedAppsCount;

        [ObservableProperty]
        private string? _selectedDeviceSerial;

        [ObservableProperty]
        private double _exportProgress;

        [ObservableProperty]
        private string _exportStatusText = "Preparing export...";

        [ObservableProperty]
        private string _exportProgressText = string.Empty;

        [ObservableProperty]
        private string _currentExportFile = string.Empty;

        /// <summary>
        /// Constructor for InstalledAppViewModel
        /// </summary>
        public InstalledAppViewModel(
            AppInventoryService appInventoryService,
            ApkExportService apkExportService,
            AdbService adbService)
        {
            _appInventoryService = appInventoryService ?? throw new ArgumentNullException(nameof(appInventoryService));
            _apkExportService = apkExportService ?? throw new ArgumentNullException(nameof(apkExportService));
            _adbService = adbService ?? throw new ArgumentNullException(nameof(adbService));

            // Initialize commands
            LoadAppsCommand = new AsyncRelayCommand(LoadAppsAsync, () => !IsLoading && !string.IsNullOrEmpty(SelectedDeviceSerial));
            RefreshAppsCommand = new AsyncRelayCommand(RefreshAppsAsync, () => !IsLoading);
            ExportSelectedAppsCommand = new AsyncRelayCommand(ExportSelectedAppsAsync, () => !IsExporting && SelectedAppsCount > 0);
            ExportAllFilteredAppsCommand = new AsyncRelayCommand(ExportAllFilteredAppsAsync, () => !IsExporting && FilteredApps.Count > 0);
            SelectAllCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(SelectAllApps, () => FilteredApps.Count > 0);
            DeselectAllCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(DeselectAllApps, () => SelectedAppsCount > 0);
            CancelLoadCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(CancelLoad, () => IsLoading);
            CancelExportCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(CancelExport, () => IsExporting);

            // Subscribe to property changes for filtering
            PropertyChanged += OnPropertyChanged;
        }

        #region Commands

        public IAsyncRelayCommand LoadAppsCommand { get; } = null!;
        public IAsyncRelayCommand RefreshAppsCommand { get; } = null!;
        public IAsyncRelayCommand ExportSelectedAppsCommand { get; } = null!;
        public IAsyncRelayCommand ExportAllFilteredAppsCommand { get; } = null!;
        public IRelayCommand SelectAllCommand { get; } = null!;
        public IRelayCommand DeselectAllCommand { get; } = null!;
        public IRelayCommand CancelLoadCommand { get; } = null!;
        public IRelayCommand CancelExportCommand { get; } = null!;

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the selected device for app operations
        /// </summary>
        public void SetSelectedDevice(string deviceSerial)
        {
            SelectedDeviceSerial = deviceSerial;
            LoadAppsCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Clears all loaded apps and resets the view
        /// </summary>
        public void ClearApps()
        {
            InstalledApps.Clear();
            FilteredApps.Clear();
            TotalAppsCount = 0;
            SelectedAppsCount = 0;
            StatusMessage = "No device selected";
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Loads installed apps from the selected device with progress reporting
        /// </summary>
        private async Task LoadAppsAsync()
        {
            if (string.IsNullOrEmpty(SelectedDeviceSerial))
            {
                StatusMessage = "No device selected";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Initializing app scan...";
                ExportLog = string.Empty;

                _loadCancellationTokenSource?.Cancel();
                _loadCancellationTokenSource = new CancellationTokenSource();

                // Configure query options based on current filter
                var queryOptions = new AppQueryOptions
                {
                    UserId = 0,
                    OnlyUserApps = SelectedFilterType == AppFilterType.UserApps,
                    IncludeSystemApps = SelectedFilterType != AppFilterType.UserApps,
                    KeywordFilter = string.Empty, // Apply filtering after loading for better performance
                    WithSizes = false // Skip sizes initially for faster loading
                };

                StatusMessage = "Scanning device packages...";
                
                // Load apps from device with progress updates
                var apps = await _appInventoryService.ListInstalledAppsAsync(
                    SelectedDeviceSerial,
                    queryOptions,
                    _loadCancellationTokenSource.Token);

                StatusMessage = "Processing app information...";
                
                // Clear and update collections efficiently
                InstalledApps.Clear();
                
                // Add apps in batches to improve UI responsiveness
                const int batchSize = 20;
                for (int i = 0; i < apps.Count; i += batchSize)
                {
                    var batch = apps.Skip(i).Take(batchSize);
                    foreach (var app in batch)
                    {
                        InstalledApps.Add(app);
                    }
                    
                    // Update progress
                    var progress = Math.Min(100, (i + batchSize) * 100 / apps.Count);
                    StatusMessage = $"Loading apps... {progress}%";
                    
                    // Allow UI updates
                    await Task.Delay(1, _loadCancellationTokenSource.Token);
                }

                TotalAppsCount = InstalledApps.Count;
                ApplyFilters();

                StatusMessage = $"Loaded {TotalAppsCount} apps successfully";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Loading cancelled";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading apps: {ex.Message}";
                AppendToLog($"❌ Error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                LoadAppsCommand.NotifyCanExecuteChanged();
                RefreshAppsCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// Refreshes the app list
        /// </summary>
        private async Task RefreshAppsAsync()
        {
            await LoadAppsAsync();
        }

        /// <summary>
        /// Exports selected apps to a chosen directory
        /// </summary>
        private async Task ExportSelectedAppsAsync()
        {
            var selectedApps = FilteredApps.Where(app => app.IsSelected).ToList();
            if (selectedApps.Count == 0)
            {
                StatusMessage = "No apps selected for export";
                return;
            }

            await ExportAppsAsync(selectedApps);
        }

        /// <summary>
        /// Exports all filtered apps to a chosen directory
        /// </summary>
        private async Task ExportAllFilteredAppsAsync()
        {
            if (FilteredApps.Count == 0)
            {
                StatusMessage = "No apps to export";
                return;
            }

            await ExportAppsAsync(FilteredApps.ToList());
        }

        /// <summary>
        /// Common method for exporting apps with progress tracking
        /// </summary>
        private async Task ExportAppsAsync(List<InstalledApp> appsToExport)
        {
            // Choose export directory
            var folderDialog = new VistaFolderBrowserDialog
            {
                Description = "Select folder to export APK files",
                UseDescriptionForTitle = true
            };

            if (folderDialog.ShowDialog() != true)
                return;

            var exportPath = folderDialog.SelectedPath;

            try
            {
                IsExporting = true;
                ExportLog = string.Empty;
                ExportProgress = 0;
                ExportStatusText = "Initializing export...";
                ExportProgressText = $"0 of {appsToExport.Count} apps";
                CurrentExportFile = string.Empty;
                StatusMessage = $"Exporting {appsToExport.Count} app(s)...";

                _exportCancellationTokenSource?.Cancel();
                _exportCancellationTokenSource = new CancellationTokenSource();

                var packageNames = appsToExport.Select(app => app.PackageName).ToList();
                var totalApps = packageNames.Count;
                var processedApps = 0;

                // Create enhanced progress reporter
                var progressReporter = new Progress<string>(message =>
                {
                    AppendToLog(message);
                    
                    // Parse progress from message
                    if (message.Contains("Exporting") && message.Contains(".apk"))
                    {
                        processedApps++;
                        var progress = (double)processedApps / totalApps * 100;
                        ExportProgress = Math.Min(100, progress);
                        ExportProgressText = $"{processedApps} of {totalApps} apps";
                        ExportStatusText = processedApps == totalApps ? "Finalizing export..." : "Exporting APK files...";
                        
                        // Extract filename from message
                        var parts = message.Split(' ');
                        var apkFile = parts.FirstOrDefault(p => p.EndsWith(".apk"));
                        if (!string.IsNullOrEmpty(apkFile))
                        {
                            CurrentExportFile = $"Current: {Path.GetFileName(apkFile)}";
                        }
                    }
                    else if (message.Contains("Starting export"))
                    {
                        ExportStatusText = "Starting export process...";
                        CurrentExportFile = "Preparing files...";
                    }
                });

                // Export apps
                var result = await _apkExportService.ExportMultipleApksAsync(
                    SelectedDeviceSerial!,
                    packageNames,
                    exportPath,
                    IncludeSplits,
                    progressReporter,
                    _exportCancellationTokenSource.Token);

                if (result.Success)
                {
                    ExportProgress = 100;
                    ExportStatusText = "Export completed successfully!";
                    ExportProgressText = $"{result.ExportedPaths.Count} files exported";
                    CurrentExportFile = $"Saved to: {Path.GetFileName(exportPath)}";
                    StatusMessage = $"Export completed: {result.ExportedPaths.Count} file(s) exported";
                    AppendToLog($"🎉 Export completed successfully!");
                    AppendToLog($"📁 Files saved to: {exportPath}");
                }
                else
                {
                    ExportStatusText = "Export failed";
                    ExportProgressText = result.ErrorMessage ?? "Unknown error";
                    CurrentExportFile = string.Empty;
                    StatusMessage = $"Export failed: {result.ErrorMessage}";
                    AppendToLog($"❌ Export failed: {result.ErrorMessage}");
                }
            }
            catch (OperationCanceledException)
            {
                ExportProgress = 0;
                ExportStatusText = "Export cancelled";
                ExportProgressText = "Operation cancelled by user";
                CurrentExportFile = string.Empty;
                StatusMessage = "Export cancelled";
                AppendToLog("⏹️ Export cancelled by user");
            }
            catch (Exception ex)
            {
                ExportProgress = 0;
                ExportStatusText = "Export error";
                ExportProgressText = ex.Message;
                CurrentExportFile = string.Empty;
                StatusMessage = $"Export error: {ex.Message}";
                AppendToLog($"❌ Export error: {ex.Message}");
            }
            finally
            {
                IsExporting = false;
                ExportSelectedAppsCommand.NotifyCanExecuteChanged();
                ExportAllFilteredAppsCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// Selects all filtered apps with batch updates
        /// </summary>
        private void SelectAllApps()
        {
            // Batch update for better performance
            var appsToUpdate = FilteredApps.ToList();
            
            Task.Run(() =>
            {
                foreach (var app in appsToUpdate)
                {
                    app.IsSelected = true;
                }
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    UpdateSelectedCount();
                    UpdateCommandStates();
                });
            });
        }

        /// <summary>
        /// Deselects all apps with batch updates
        /// </summary>
        private void DeselectAllApps()
        {
            // Batch update for better performance
            var appsToUpdate = InstalledApps.ToList();
            
            Task.Run(() =>
            {
                foreach (var app in appsToUpdate)
                {
                    app.IsSelected = false;
                }
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    UpdateSelectedCount();
                    UpdateCommandStates();
                });
            });
        }

        /// <summary>
        /// Cancels the current loading operation
        /// </summary>
        private void CancelLoad()
        {
            _loadCancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Cancels the current export operation
        /// </summary>
        private void CancelExport()
        {
            _exportCancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Applies filters to the installed apps list with optimized performance
        /// </summary>
        private void ApplyFilters()
        {
            // Use async filtering for large collections
            Task.Run(() =>
            {
                var filtered = InstalledApps.AsEnumerable();

                // Apply app type filter
                switch (SelectedFilterType)
                {
                    case AppFilterType.UserApps:
                        filtered = filtered.Where(app => !app.IsSystemApp);
                        break;
                    case AppFilterType.SystemApps:
                        filtered = filtered.Where(app => app.IsSystemApp);
                        break;
                    case AppFilterType.All:
                    default:
                        // No filtering by type
                        break;
                }

                // Apply keyword filter with optimized string comparison
                if (!string.IsNullOrWhiteSpace(SearchKeyword))
                {
                    var keyword = SearchKeyword.Trim().ToLowerInvariant();
                    filtered = filtered.Where(app =>
                        app.PackageName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                        app.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                }

                // Sort and convert to list
                var sortedApps = filtered.OrderBy(app => app.DisplayName).ToList();

                // Update UI on main thread
                App.Current.Dispatcher.Invoke(() =>
                {
                    FilteredApps.Clear();
                    foreach (var app in sortedApps)
                    {
                        FilteredApps.Add(app);
                    }

                    UpdateSelectedCount();
                    UpdateCommandStates();
                });
            });
        }

        /// <summary>
        /// Updates the count of selected apps
        /// </summary>
        private void UpdateSelectedCount()
        {
            SelectedAppsCount = InstalledApps.Where(app => app.IsSelected).Count();
        }

        /// <summary>
        /// Updates the enabled state of commands
        /// </summary>
        private void UpdateCommandStates()
        {
            SelectAllCommand.NotifyCanExecuteChanged();
            DeselectAllCommand.NotifyCanExecuteChanged();
            ExportSelectedAppsCommand.NotifyCanExecuteChanged();
            ExportAllFilteredAppsCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Appends a message to the export log
        /// </summary>
        private void AppendToLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            ExportLog += $"[{timestamp}] {message}\n";
        }

        /// <summary>
        /// Handles property changes for filtering and UI updates
        /// </summary>
        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SearchKeyword):
                case nameof(SelectedFilterType):
                    ApplyFilters();
                    break;
                case nameof(IsLoading):
                case nameof(IsExporting):
                    UpdateCommandStates();
                    break;
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Disposes resources when the ViewModel is no longer needed
        /// </summary>
        public void Dispose()
        {
            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource?.Dispose();
            _exportCancellationTokenSource?.Cancel();
            _exportCancellationTokenSource?.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// Enum for app filtering options
    /// </summary>
    public enum AppFilterType
    {
        All,
        UserApps,
        SystemApps
    }
}
