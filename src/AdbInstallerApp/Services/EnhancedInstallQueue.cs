using AdbInstallerApp.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AdbInstallerApp.Services
{
    /// <summary>
    /// Enhanced Install Queue with concurrent operations, pause/resume, and smart scheduling
    /// </summary>
    public class EnhancedInstallQueue : IDisposable
    {
        private readonly OptimizedProgressService _progressService;
        private readonly InstallOrchestrator _installer;
        private readonly ConcurrentQueue<QueuedInstallOperation> _installQueue = new();
        private readonly ConcurrentDictionary<string, QueuedInstallOperation> _activeOperations = new();
        private readonly SemaphoreSlim _concurrencyLimiter;
        private readonly CancellationTokenSource _globalCancellation = new();
        
        // Keyboard shortcuts
        private readonly Dictionary<Key, Action> _keyboardShortcuts = new();
        
        // Performance settings
        private readonly int _maxConcurrentOperations;
        private volatile bool _isPaused = false;
        
        public event EventHandler<InstallQueueEventArgs>? OperationCompleted;
        public event EventHandler<InstallQueueEventArgs>? OperationFailed;
        public event EventHandler<InstallQueueEventArgs>? QueueEmpty;
        
        public bool IsPaused => _isPaused;
        public int QueuedOperationsCount => _installQueue.Count;
        public int ActiveOperationsCount => _activeOperations.Count;
        
        public EnhancedInstallQueue(InstallOrchestrator installer, int maxConcurrentOperations = 2)
        {
            _installer = installer ?? throw new ArgumentNullException(nameof(installer));
            _progressService = OptimizedProgressService.Instance;
            _maxConcurrentOperations = Math.Max(1, maxConcurrentOperations);
            _concurrencyLimiter = new SemaphoreSlim(_maxConcurrentOperations);
            
            InitializeKeyboardShortcuts();
            StartQueueProcessor();
        }
        
        #region Queue Management
        
        /// <summary>
        /// Add single APK installation to queue
        /// </summary>
        public string QueueApkInstallation(
            IEnumerable<DeviceInfo> devices,
            IEnumerable<ApkItem> apks,
            InstallOptions options,
            OptimizedProgressService.OperationPriority priority = OptimizedProgressService.OperationPriority.Normal)
        {
            var operation = new QueuedInstallOperation
            {
                Id = OptimizedProgressService.GenerateOperationId("apk-install"),
                Type = InstallOperationType.SingleApk,
                Devices = devices.ToList(),
                ApkItems = apks.ToList(),
                Options = options,
                Priority = priority,
                QueuedTime = DateTime.Now,
                EstimatedDuration = EstimateInstallDuration(devices.Count(), apks.Sum(a => GetFileSize(a.FilePath)))
            };
            
            _installQueue.Enqueue(operation);
            return operation.Id;
        }
        
        /// <summary>
        /// Add APK group installation to queue
        /// </summary>
        public string QueueGroupInstallation(
            IEnumerable<DeviceInfo> devices,
            IEnumerable<ApkGroup> groups,
            InstallOptions options,
            OptimizedProgressService.OperationPriority priority = OptimizedProgressService.OperationPriority.Normal)
        {
            var operation = new QueuedInstallOperation
            {
                Id = OptimizedProgressService.GenerateOperationId("group-install"),
                Type = InstallOperationType.GroupInstall,
                Devices = devices.ToList(),
                ApkGroups = groups.ToList(),
                Options = options,
                Priority = priority,
                QueuedTime = DateTime.Now,
                EstimatedDuration = EstimateGroupInstallDuration(devices.Count(), groups)
            };
            
            _installQueue.Enqueue(operation);
            return operation.Id;
        }
        
        /// <summary>
        /// Pause all operations
        /// </summary>
        public void PauseAll()
        {
            _isPaused = true;
            foreach (var operation in _activeOperations.Values)
            {
                _progressService.PauseOperation(operation.Id);
            }
        }
        
        /// <summary>
        /// Resume all operations
        /// </summary>
        public void ResumeAll()
        {
            _isPaused = false;
            foreach (var operation in _activeOperations.Values)
            {
                _progressService.ResumeOperation(operation.Id);
            }
        }
        
        /// <summary>
        /// Cancel specific operation
        /// </summary>
        public void CancelOperation(string operationId)
        {
            if (_activeOperations.TryGetValue(operationId, out var operation))
            {
                operation.CancellationToken.Cancel();
                _progressService.CancelOperation(operationId);
            }
        }
        
        /// <summary>
        /// Cancel all operations
        /// </summary>
        public void CancelAll()
        {
            _globalCancellation.Cancel();
            
            foreach (var operation in _activeOperations.Values)
            {
                operation.CancellationToken.Cancel();
                _progressService.CancelOperation(operation.Id);
            }
            
            // Clear queue
            while (_installQueue.TryDequeue(out _)) { }
        }
        
        #endregion
        
        #region Queue Processing
        
        private void StartQueueProcessor()
        {
            Task.Run(async () =>
            {
                while (!_globalCancellation.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (!_isPaused && _installQueue.TryDequeue(out var operation))
                        {
                            await _concurrencyLimiter.WaitAsync(_globalCancellation.Token);
                            
                            // Start operation in background
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await ProcessOperation(operation);
                                }
                                finally
                                {
                                    _concurrencyLimiter.Release();
                                }
                            });
                        }
                        else
                        {
                            await Task.Delay(100, _globalCancellation.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
        }
        
        private async Task ProcessOperation(QueuedInstallOperation operation)
        {
            operation.CancellationToken = new CancellationTokenSource();
            _activeOperations[operation.Id] = operation;
            
            try
            {
                operation.StartTime = DateTime.Now;
                
                // Determine operation icon and name
                var (icon, name) = GetOperationIconAndName(operation);
                
                // Start progress tracking
                _progressService.StartOperation(name, "Preparing installation...", operation.Priority, icon);
                
                // Execute based on operation type
                switch (operation.Type)
                {
                    case InstallOperationType.SingleApk:
                        await ExecuteApkInstallation(operation);
                        break;
                        
                    case InstallOperationType.GroupInstall:
                        await ExecuteGroupInstallation(operation);
                        break;
                }
                
                operation.EndTime = DateTime.Now;
                operation.Status = InstallOperationStatus.Completed;
                
                _progressService.CompleteOperation(operation.Id, "Installation completed successfully");
                OperationCompleted?.Invoke(this, new InstallQueueEventArgs(operation));
            }
            catch (OperationCanceledException)
            {
                operation.Status = InstallOperationStatus.Cancelled;
                _progressService.CancelOperation(operation.Id);
            }
            catch (Exception ex)
            {
                operation.Status = InstallOperationStatus.Failed;
                operation.ErrorMessage = ex.Message;
                _progressService.CancelOperation(operation.Id);
                OperationFailed?.Invoke(this, new InstallQueueEventArgs(operation));
            }
            finally
            {
                _activeOperations.TryRemove(operation.Id, out _);
                operation.CancellationToken?.Dispose();
                
                if (_installQueue.IsEmpty && _activeOperations.IsEmpty)
                {
                    QueueEmpty?.Invoke(this, new InstallQueueEventArgs(operation));
                }
            }
        }
        
        private async Task ExecuteApkInstallation(QueuedInstallOperation operation)
        {
            await _installer.InstallAsync(
                operation.Devices,
                operation.ApkItems,
                operation.Options.Reinstall,
                operation.Options.GrantPermissions,
                operation.Options.AllowDowngrade,
                msg => { /* Log message */ },
                operation.CancellationToken.Token);
        }
        
        private async Task ExecuteGroupInstallation(QueuedInstallOperation operation)
        {
            await _installer.InstallGroupsAsync(
                operation.Devices,
                operation.ApkGroups,
                operation.Options.Reinstall,
                operation.Options.GrantPermissions,
                operation.Options.AllowDowngrade,
                msg => { /* Log message */ },
                operation.CancellationToken.Token);
        }
        
        #endregion
        
        #region Smart Estimation
        
        private TimeSpan EstimateInstallDuration(int deviceCount, long totalBytes)
        {
            // Base time per APK installation
            const double baseSecondsPerApk = 5.0;
            
            // Network speed factor (bytes per second)
            const double avgNetworkSpeed = 10 * 1024 * 1024; // 10 MB/s
            
            // Device performance factor
            const double deviceFactor = 1.2; // Slower devices take 20% longer
            
            var networkTime = totalBytes / avgNetworkSpeed;
            var installTime = deviceCount * baseSecondsPerApk * deviceFactor;
            
            return TimeSpan.FromSeconds(networkTime + installTime);
        }
        
        private TimeSpan EstimateGroupInstallDuration(int deviceCount, IEnumerable<ApkGroup> groups)
        {
            var totalBytes = groups.SelectMany(g => g.ApkItems)
                                  .Sum(apk => GetFileSize(apk.FilePath));
            
            var totalApks = groups.SelectMany(g => g.ApkItems).Count();
            
            // Group installations are sequential, so multiply by device count
            var baseEstimate = EstimateInstallDuration(1, totalBytes);
            return TimeSpan.FromSeconds(baseEstimate.TotalSeconds * deviceCount * totalApks);
        }
        
        private long GetFileSize(string filePath)
        {
            try
            {
                return new FileInfo(filePath).Length;
            }
            catch
            {
                return 10 * 1024 * 1024; // Default 10MB if can't read
            }
        }
        
        private (string icon, string name) GetOperationIconAndName(QueuedInstallOperation operation)
        {
            return operation.Type switch
            {
                InstallOperationType.SingleApk => ("📱", $"Installing {operation.ApkItems.Count} APK(s)"),
                InstallOperationType.GroupInstall => ("📦", $"Installing {operation.ApkGroups.Count} Group(s)"),
                _ => ("⚡", "Installing")
            };
        }
        
        #endregion
        
        #region Keyboard Shortcuts
        
        private void InitializeKeyboardShortcuts()
        {
            _keyboardShortcuts[Key.C] = () => // Ctrl+C to cancel
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    CancelAll();
                }
            };
            
            _keyboardShortcuts[Key.P] = () => // Ctrl+P to pause/resume
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (_isPaused)
                        ResumeAll();
                    else
                        PauseAll();
                }
            };
        }
        
        public void HandleKeyPress(Key key)
        {
            if (_keyboardShortcuts.TryGetValue(key, out var action))
            {
                action.Invoke();
            }
        }
        
        #endregion
        
        #region Supporting Classes
        
        public class QueuedInstallOperation
        {
            public string Id { get; set; } = "";
            public InstallOperationType Type { get; set; }
            public List<DeviceInfo> Devices { get; set; } = new();
            public List<ApkItem> ApkItems { get; set; } = new();
            public List<ApkGroup> ApkGroups { get; set; } = new();
            public InstallOptions Options { get; set; } = new();
            public OptimizedProgressService.OperationPriority Priority { get; set; }
            public DateTime QueuedTime { get; set; }
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public TimeSpan EstimatedDuration { get; set; }
            public InstallOperationStatus Status { get; set; } = InstallOperationStatus.Queued;
            public string ErrorMessage { get; set; } = "";
            public CancellationTokenSource? CancellationToken { get; set; }
        }
        
        public class InstallOptions
        {
            public bool Reinstall { get; set; }
            public bool GrantPermissions { get; set; }
            public bool AllowDowngrade { get; set; }
            
            public InstallOptions(bool reinstall = false, bool grantPermissions = false, bool allowDowngrade = false)
            {
                Reinstall = reinstall;
                GrantPermissions = grantPermissions;
                AllowDowngrade = allowDowngrade;
            }
        }
        
        public enum InstallOperationType
        {
            SingleApk,
            GroupInstall
        }
        
        public enum InstallOperationStatus
        {
            Queued,
            Running,
            Paused,
            Completed,
            Cancelled,
            Failed
        }
        
        public class InstallQueueEventArgs : EventArgs
        {
            public QueuedInstallOperation Operation { get; }
            
            public InstallQueueEventArgs(QueuedInstallOperation operation)
            {
                Operation = operation;
            }
        }
        
        #endregion
        
        public void Dispose()
        {
            _globalCancellation.Cancel();
            _globalCancellation.Dispose();
            _concurrencyLimiter.Dispose();
            
            foreach (var operation in _activeOperations.Values)
            {
                operation.CancellationToken?.Cancel();
                operation.CancellationToken?.Dispose();
            }
        }
    }
}
