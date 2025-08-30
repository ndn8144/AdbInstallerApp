using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AdbInstallerApp.Events;
using AdbInstallerApp.Models;
using AdbInstallerApp.Services;

namespace AdbInstallerApp.ViewModels
{
    /// <summary>
    /// ViewModel for advanced installation orchestration with preflight validation
    /// </summary>
    public partial class InstallationViewModel : ObservableObject, IDisposable
    {
        private readonly AdvancedInstallOrchestrator _orchestrator;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        // Observable Properties
        [ObservableProperty]
        private bool _isInstalling = false;
        
        [ObservableProperty]
        private bool _isPlanningPhase = false;
        
        [ObservableProperty]
        private double _overallProgress = 0;
        
        [ObservableProperty]
        private string _currentOperation = "";
        
        [ObservableProperty]
        private string _currentDevice = "";
        
        [ObservableProperty]
        private int _currentDeviceIndex = 0;
        
        [ObservableProperty]
        private int _totalDevices = 0;
        
        [ObservableProperty]
        private int _currentUnitIndex = 0;
        
        [ObservableProperty]
        private int _totalUnits = 0;
        
        [ObservableProperty]
        private string _progressText = "";
        
        [ObservableProperty]
        private string _statusText = "Ready";
        
        [ObservableProperty]
        private InstallationSummary? _lastSummary;
        
        // Collections
        public ObservableCollection<DeviceInstallPlan> InstallationPlans { get; } = new();
        public ObservableCollection<InstallationEventViewModel> InstallationLog { get; } = new();
        public ObservableCollection<string> SelectedDeviceSerials { get; } = new();
        public ObservableCollection<SelectedItem> SelectedItems { get; } = new();
        
        // Install Options
        [ObservableProperty]
        private Models.InstallOptions _installOptions = new Models.InstallOptions();
        
        // Commands
        public IAsyncRelayCommand BuildPlansCommand { get; }
        public IAsyncRelayCommand StartInstallationCommand { get; }
        public IRelayCommand CancelInstallationCommand { get; }
        public IRelayCommand ClearLogCommand { get; }
        
        // Computed Properties
        public bool CanBuildPlans => !IsInstalling && !IsPlanningPhase && 
            SelectedDeviceSerials.Count > 0 && SelectedItems.Count > 0;
            
        public bool CanStartInstallation => !IsInstalling && !IsPlanningPhase && 
            InstallationPlans.Count > 0 && InstallationPlans.Any(p => p.CanExecute);
            
        public bool CanCancelInstallation => IsInstalling || IsPlanningPhase;
        
        public string InstallationButtonText => IsInstalling ? "Installing..." : 
            IsPlanningPhase ? "Planning..." : "Start Installation";
            
        public string PlansStatusText => InstallationPlans.Count > 0 ? 
            $"{InstallationPlans.Count} plan(s) ready, {InstallationPlans.Sum(p => p.TotalFiles)} file(s)" : 
            "No plans built";
        
        public InstallationViewModel(AdvancedInstallOrchestrator orchestrator)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Subscribe to orchestrator events
            _orchestrator.InstallationEvent += OnInstallationEvent;
            _orchestrator.ProgressUpdated += OnProgressUpdated;
            
            // Initialize commands
            BuildPlansCommand = new AsyncRelayCommand(BuildPlansAsync, () => CanBuildPlans);
            StartInstallationCommand = new AsyncRelayCommand(StartInstallationAsync, () => CanStartInstallation);
            CancelInstallationCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(CancelInstallation, () => CanCancelInstallation);
            ClearLogCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ClearLog);
            
            // Subscribe to property changes to update command states
            PropertyChanged += OnPropertyChanged;
            SelectedDeviceSerials.CollectionChanged += (s, e) => UpdateCommandStates();
            SelectedItems.CollectionChanged += (s, e) => UpdateCommandStates();
            InstallationPlans.CollectionChanged += (s, e) => UpdateCommandStates();
        }
        
        private async Task BuildPlansAsync()
        {
            try
            {
                IsPlanningPhase = true;
                StatusText = "Building installation plans...";
                InstallationPlans.Clear();
                
                var plans = await _orchestrator.BuildInstallationPlansAsync(
                    SelectedDeviceSerials,
                    SelectedItems,
                    InstallOptions,
                    _cancellationTokenSource.Token);
                
                foreach (var plan in plans)
                {
                    InstallationPlans.Add(plan);
                }
                
                StatusText = plans.Count > 0 ? 
                    $"Built {plans.Count} installation plan(s)" : 
                    "No valid installation plans could be built";
                    
                UpdateProgressText();
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to build plans: {ex.Message}";
                AddLogEntry(new ErrorEvent(DateTime.Now, "", $"Plan building failed: {ex.Message}"));
            }
            finally
            {
                IsPlanningPhase = false;
                UpdateCommandStates();
            }
        }
        
        private async Task StartInstallationAsync()
        {
            try
            {
                IsInstalling = true;
                StatusText = "Installing...";
                OverallProgress = 0;
                
                var executablePlans = InstallationPlans.Where(p => p.CanExecute).ToList();
                TotalDevices = executablePlans.Count;
                TotalUnits = executablePlans.Sum(p => p.Units.Count);
                
                var summary = await _orchestrator.ExecuteInstallationPlansAsync(
                    executablePlans,
                    _cancellationTokenSource.Token);
                
                LastSummary = summary;
                StatusText = summary.IsSuccess ? 
                    $"Installation completed successfully: {summary.GetSummary()}" :
                    $"Installation completed with errors: {summary.GetSummary()}";
                    
                OverallProgress = 100;
            }
            catch (OperationCanceledException)
            {
                StatusText = "Installation cancelled";
                OverallProgress = 0;
            }
            catch (Exception ex)
            {
                StatusText = $"Installation failed: {ex.Message}";
                AddLogEntry(new ErrorEvent(DateTime.Now, "", $"Installation failed: {ex.Message}"));
            }
            finally
            {
                IsInstalling = false;
                CurrentOperation = "";
                CurrentDevice = "";
                UpdateCommandStates();
            }
        }
        
        private void CancelInstallation()
        {
            _cancellationTokenSource.Cancel();
            _orchestrator.CancelAll();
            StatusText = "Cancelling...";
        }
        
        private void ClearLog()
        {
            InstallationLog.Clear();
        }
        
        private void OnInstallationEvent(object? sender, InstallationEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                AddLogEntry(e.Event);
            });
        }
        
        private void OnProgressUpdated(object? sender, InstallationProgressArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                CurrentDevice = e.Serial;
                CurrentDeviceIndex = e.CurrentDevice;
                TotalDevices = e.TotalDevices;
                CurrentUnitIndex = e.CurrentUnit;
                TotalUnits = e.TotalUnits;
                CurrentOperation = e.CurrentOperation;
                OverallProgress = e.OverallProgress;
                
                UpdateProgressText();
            });
        }
        
        private void AddLogEntry(InstallationEvent evt)
        {
            var viewModel = new InstallationEventViewModel(evt);
            InstallationLog.Add(viewModel);
            
            // Keep log size manageable
            if (InstallationLog.Count > 1000)
            {
                InstallationLog.RemoveAt(0);
            }
        }
        
        private void UpdateProgressText()
        {
            if (IsInstalling)
            {
                ProgressText = $"Device {CurrentDeviceIndex}/{TotalDevices}, Unit {CurrentUnitIndex}/{TotalUnits}";
            }
            else if (IsPlanningPhase)
            {
                ProgressText = "Building installation plans...";
            }
            else if (InstallationPlans.Count > 0)
            {
                var totalFiles = InstallationPlans.Sum(p => p.TotalFiles);
                var totalSize = InstallationPlans.Sum(p => p.TotalSize);
                ProgressText = $"{InstallationPlans.Count} plan(s), {totalFiles} file(s), {FormatBytes(totalSize)}";
            }
            else
            {
                ProgressText = "No installation plans";
            }
        }
        
        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(IsInstalling) or nameof(IsPlanningPhase))
            {
                UpdateCommandStates();
            }
        }
        
        private void UpdateCommandStates()
        {
            BuildPlansCommand.NotifyCanExecuteChanged();
            StartInstallationCommand.NotifyCanExecuteChanged();
            CancelInstallationCommand.NotifyCanExecuteChanged();
            
            OnPropertyChanged(nameof(CanBuildPlans));
            OnPropertyChanged(nameof(CanStartInstallation));
            OnPropertyChanged(nameof(CanCancelInstallation));
            OnPropertyChanged(nameof(InstallationButtonText));
            OnPropertyChanged(nameof(PlansStatusText));
        }
        
        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
        
        public void Dispose()
        {
            _orchestrator.InstallationEvent -= OnInstallationEvent;
            _orchestrator.ProgressUpdated -= OnProgressUpdated;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _orchestrator?.Dispose();
        }
    }
    
    /// <summary>
    /// ViewModel for displaying installation events in the log
    /// </summary>
    public class InstallationEventViewModel : ObservableObject
    {
        public DateTime Timestamp { get; }
        public string Serial { get; }
        public string Message { get; }
        public string EventType { get; }
        public string Icon { get; }
        public string TimeText { get; }
        
        public InstallationEventViewModel(InstallationEvent evt)
        {
            Timestamp = evt.Timestamp;
            Serial = evt.Serial;
            Message = evt.Message;
            TimeText = evt.Timestamp.ToString("HH:mm:ss");
            
            (EventType, Icon) = evt switch
            {
                InfoEvent => ("Info", "ℹ️"),
                SuccessEvent => ("Success", "✅"),
                WarningEvent => ("Warning", "⚠️"),
                ErrorEvent => ("Error", "❌"),
                CommandEvent => ("Command", "⚡"),
                _ => ("Unknown", "📝")
            };
        }
    }
}
