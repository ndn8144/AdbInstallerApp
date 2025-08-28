using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace AdbInstallerApp.Services
{
    /// <summary>
    /// Optimized progress service with throttling, concurrent operations, and enhanced features
    /// </summary>
    public sealed partial class OptimizedProgressService : ObservableObject
    {
        private static readonly Lazy<OptimizedProgressService> _instance = 
            new(() => new OptimizedProgressService());
        
        public static OptimizedProgressService Instance => _instance.Value;
        
        // UI Throttling - max 10 updates/second
        private readonly Timer _uiUpdateTimer;
        private readonly object _updateLock = new object();
        private volatile bool _hasPendingUpdate = false;
        private const int UI_UPDATE_INTERVAL_MS = 100; // 10 updates/second
        
        // Concurrent Operations Support
        private readonly ConcurrentDictionary<string, OperationInfo> _activeOperations = new();
        private readonly ConcurrentQueue<OperationUpdate> _pendingUpdates = new();
        private readonly SemaphoreSlim _operationSemaphore = new(Environment.ProcessorCount);
        
        // Memory Pooling
        private readonly ConcurrentQueue<OperationUpdate> _updatePool = new();
        private const int MAX_POOLED_UPDATES = 100;
        
        // Operation History
        private readonly ConcurrentQueue<OperationHistoryItem> _operationHistory = new();
        private const int MAX_HISTORY_ITEMS = 1000;
        
        // Pause/Resume Support
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _pauseTokens = new();
        
        #region Observable Properties
        
        [ObservableProperty]
        private bool _isOperationInProgress;
        
        [ObservableProperty]
        private double _currentProgress;
        
        [ObservableProperty]
        private string _currentOperationName = "";
        
        [ObservableProperty]
        private string _currentOperationDetail = "";
        
        [ObservableProperty]
        private string _estimatedTimeRemaining = "";
        
        [ObservableProperty]
        private string _processingSpeed = "";
        
        [ObservableProperty]
        private int _activeOperationsCount;
        
        [ObservableProperty]
        private string _operationIcon = "⚡";
        
        // Computed Properties
        public string ProgressText => $"{CurrentProgress:F1}%";
        public string DetailedProgressInfo => $"{CurrentOperationName} - {CurrentOperationDetail}";
        public bool CanPauseResume => IsOperationInProgress;
        
        #endregion
        
        #region Supporting Classes
        
        public class OperationInfo
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Detail { get; set; } = "";
            public double Progress { get; set; }
            public DateTime StartTime { get; set; }
            public OperationPriority Priority { get; set; }
            public OperationStatus Status { get; set; }
            public long TotalBytes { get; set; }
            public long ProcessedBytes { get; set; }
            public int TotalItems { get; set; }
            public int CompletedItems { get; set; }
            public bool IsPaused { get; set; }
            public string Icon { get; set; } = "⚡";
        }
        
        public class OperationUpdate
        {
            public string OperationId { get; set; } = "";
            public double Progress { get; set; }
            public string Detail { get; set; } = "";
            public int TotalItems { get; set; }
            public int CompletedItems { get; set; }
            public long TotalBytes { get; set; }
            public long ProcessedBytes { get; set; }
            public DateTime Timestamp { get; set; }
        }
        
        public class OperationHistoryItem
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public DateTime StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public OperationStatus Status { get; set; }
            public string Result { get; set; } = "";
            public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
        }
        
        public enum OperationPriority
        {
            Low = 0,
            Normal = 1,
            High = 2,
            Critical = 3
        }
        
        public enum OperationStatus
        {
            Pending,
            Running,
            Paused,
            Completed,
            Cancelled,
            Failed
        }
        
        #endregion
        
        private OptimizedProgressService()
        {
            _uiUpdateTimer = new Timer(ProcessPendingUpdates, null, UI_UPDATE_INTERVAL_MS, UI_UPDATE_INTERVAL_MS);
            
            // Pre-populate update pool
            for (int i = 0; i < MAX_POOLED_UPDATES; i++)
            {
                _updatePool.Enqueue(new OperationUpdate());
            }
        }
        
        #region Core Methods
        
        /// <summary>
        /// Start a new operation with priority support
        /// </summary>
        public string StartOperation(string name, string detail = "", OperationPriority priority = OperationPriority.Normal, string icon = "⚡")
        {
            var operationId = GenerateOperationId(name);
            var operation = new OperationInfo
            {
                Id = operationId,
                Name = name,
                Detail = detail,
                StartTime = DateTime.Now,
                Priority = priority,
                Status = OperationStatus.Running,
                Icon = icon
            };
            
            _activeOperations[operationId] = operation;
            
            // Add to history
            _operationHistory.Enqueue(new OperationHistoryItem
            {
                Id = operationId,
                Name = name,
                StartTime = operation.StartTime,
                Status = OperationStatus.Running
            });
            
            // Update UI if this is highest priority operation
            UpdateUIIfHighestPriority(operation);
            
            return operationId;
        }
        
        /// <summary>
        /// Update operation progress with smart ETA calculation
        /// </summary>
        public void UpdateProgress(string operationId, double progress, string detail = "", 
            int totalItems = 0, int completedItems = 0, long totalBytes = 0, long processedBytes = 0)
        {
            if (!_activeOperations.TryGetValue(operationId, out var operation))
                return;
                
            // Get or create update object from pool
            var update = GetPooledUpdate();
            update.OperationId = operationId;
            update.Progress = Math.Max(0, Math.Min(100, progress));
            update.Detail = detail;
            update.TotalItems = totalItems;
            update.CompletedItems = completedItems;
            update.TotalBytes = totalBytes;
            update.ProcessedBytes = processedBytes;
            update.Timestamp = DateTime.Now;
            
            _pendingUpdates.Enqueue(update);
            
            lock (_updateLock)
            {
                _hasPendingUpdate = true;
            }
        }
        
        /// <summary>
        /// Pause an operation
        /// </summary>
        public void PauseOperation(string operationId)
        {
            if (_activeOperations.TryGetValue(operationId, out var operation))
            {
                operation.IsPaused = true;
                operation.Status = OperationStatus.Paused;
                
                var pauseToken = new CancellationTokenSource();
                _pauseTokens[operationId] = pauseToken;
                
                UpdateUIIfHighestPriority(operation);
            }
        }
        
        /// <summary>
        /// Resume a paused operation
        /// </summary>
        public void ResumeOperation(string operationId)
        {
            if (_activeOperations.TryGetValue(operationId, out var operation))
            {
                operation.IsPaused = false;
                operation.Status = OperationStatus.Running;
                
                if (_pauseTokens.TryRemove(operationId, out var pauseToken))
                {
                    pauseToken.Cancel();
                    pauseToken.Dispose();
                }
                
                UpdateUIIfHighestPriority(operation);
            }
        }
        
        /// <summary>
        /// Complete an operation
        /// </summary>
        public void CompleteOperation(string operationId, string finalMessage = "Completed")
        {
            if (_activeOperations.TryRemove(operationId, out var operation))
            {
                operation.Status = OperationStatus.Completed;
                
                // Update history
                if (_operationHistory.TryPeek(out var historyItem) && historyItem.Id == operationId)
                {
                    historyItem.EndTime = DateTime.Now;
                    historyItem.Status = OperationStatus.Completed;
                    historyItem.Result = finalMessage;
                }
                
                // Show completion notification
                ShowCompletionNotification(operation.Name, finalMessage);
                
                // Update UI to next highest priority operation
                UpdateUIToNextOperation();
                
                // Auto-reset if no operations
                if (_activeOperations.IsEmpty)
                {
                    Task.Delay(2000).ContinueWith(_ => ResetProgress());
                }
            }
        }
        
        /// <summary>
        /// Cancel an operation
        /// </summary>
        public void CancelOperation(string operationId)
        {
            if (_activeOperations.TryRemove(operationId, out var operation))
            {
                operation.Status = OperationStatus.Cancelled;
                
                // Update history
                if (_operationHistory.TryPeek(out var historyItem) && historyItem.Id == operationId)
                {
                    historyItem.EndTime = DateTime.Now;
                    historyItem.Status = OperationStatus.Cancelled;
                    historyItem.Result = "Cancelled by user";
                }
                
                // Clean up pause token
                if (_pauseTokens.TryRemove(operationId, out var pauseToken))
                {
                    pauseToken.Cancel();
                    pauseToken.Dispose();
                }
                
                UpdateUIToNextOperation();
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private void ProcessPendingUpdates(object? state)
        {
            if (!_hasPendingUpdate)
                return;
                
            lock (_updateLock)
            {
                _hasPendingUpdate = false;
            }
            
            var updates = new List<OperationUpdate>();
            while (_pendingUpdates.TryDequeue(out var update))
            {
                updates.Add(update);
            }
            
            if (updates.Count == 0)
                return;
                
            // Process updates in background thread
            Task.Run(() => ProcessUpdatesInBackground(updates));
        }
        
        private void ProcessUpdatesInBackground(List<OperationUpdate> updates)
        {
            foreach (var update in updates)
            {
                if (_activeOperations.TryGetValue(update.OperationId, out var operation))
                {
                    // Update operation info
                    operation.Progress = update.Progress;
                    operation.Detail = update.Detail;
                    operation.TotalItems = update.TotalItems;
                    operation.CompletedItems = update.CompletedItems;
                    operation.TotalBytes = update.TotalBytes;
                    operation.ProcessedBytes = update.ProcessedBytes;
                    
                    // Calculate smart ETA
                    CalculateSmartETA(operation, update.Timestamp);
                    
                    // Update UI if this is the current operation
                    if (IsCurrentOperation(operation))
                    {
                        UpdateUIProperties(operation);
                    }
                }
                
                // Return update to pool
                ReturnUpdateToPool(update);
            }
        }
        
        private void CalculateSmartETA(OperationInfo operation, DateTime timestamp)
        {
            var elapsed = timestamp - operation.StartTime;
            
            if (operation.TotalBytes > 0 && operation.ProcessedBytes > 0)
            {
                // File size based ETA
                var bytesPerSecond = operation.ProcessedBytes / elapsed.TotalSeconds;
                var remainingBytes = operation.TotalBytes - operation.ProcessedBytes;
                var etaSeconds = remainingBytes / bytesPerSecond;
                
                // Processing speed calculated inline
                
                if (etaSeconds > 0 && etaSeconds < 3600)
                {
                    EstimatedTimeRemaining = TimeSpan.FromSeconds(etaSeconds).ToString(@"mm\:ss");
                }
            }
            else if (operation.TotalItems > 0 && operation.CompletedItems > 0)
            {
                // Item count based ETA
                var itemsPerSecond = operation.CompletedItems / elapsed.TotalSeconds;
                var remainingItems = operation.TotalItems - operation.CompletedItems;
                var etaSeconds = remainingItems / itemsPerSecond;
                
                // Processing speed calculated inline
                
                if (etaSeconds > 0 && etaSeconds < 3600)
                {
                    EstimatedTimeRemaining = TimeSpan.FromSeconds(etaSeconds).ToString(@"mm\:ss");
                }
            }
        }
        
        private void UpdateUIIfHighestPriority(OperationInfo operation)
        {
            var currentOp = GetHighestPriorityOperation();
            if (currentOp?.Id == operation.Id)
            {
                UpdateUIProperties(operation);
            }
        }
        
        private void UpdateUIToNextOperation()
        {
            var nextOp = GetHighestPriorityOperation();
            if (nextOp != null)
            {
                UpdateUIProperties(nextOp);
            }
            else
            {
                ResetProgress();
            }
        }
        
        private OperationInfo? GetHighestPriorityOperation()
        {
            return _activeOperations.Values
                .Where(op => op.Status == OperationStatus.Running)
                .OrderByDescending(op => op.Priority)
                .ThenBy(op => op.StartTime)
                .FirstOrDefault();
        }
        
        private bool IsCurrentOperation(OperationInfo operation)
        {
            var currentOp = GetHighestPriorityOperation();
            return currentOp?.Id == operation.Id;
        }
        
        private void UpdateUIProperties(OperationInfo operation)
        {
            CurrentProgress = operation.Progress;
            CurrentOperationName = operation.Name;
            CurrentOperationDetail = operation.Detail;
            OperationIcon = operation.Icon;
            IsOperationInProgress = true;
            ActiveOperationsCount = _activeOperations.Count;
            var processingSpeed = operation.ProcessedBytes > 0 ? 
                operation.ProcessedBytes / (DateTime.Now - operation.StartTime).TotalSeconds : 0.0;
            
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(DetailedProgressInfo));
            OnPropertyChanged(nameof(CanPauseResume));
        }
        
        private void ResetProgress()
        {
            CurrentProgress = 0;
            CurrentOperationName = "";
            CurrentOperationDetail = "";
            EstimatedTimeRemaining = "";
            ProcessingSpeed = "";
            OperationIcon = "⚡";
            IsOperationInProgress = false;
            ActiveOperationsCount = 0;
            
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(DetailedProgressInfo));
            OnPropertyChanged(nameof(CanPauseResume));
        }
        
        private OperationUpdate GetPooledUpdate()
        {
            if (_updatePool.TryDequeue(out var update))
            {
                return update;
            }
            return new OperationUpdate();
        }
        
        private void ReturnUpdateToPool(OperationUpdate update)
        {
            if (_updatePool.Count < MAX_POOLED_UPDATES)
            {
                // Reset update object
                update.OperationId = "";
                update.Progress = 0;
                update.Detail = "";
                update.TotalItems = 0;
                update.CompletedItems = 0;
                update.TotalBytes = 0;
                update.ProcessedBytes = 0;
                
                _updatePool.Enqueue(update);
            }
        }
        
        private void ShowCompletionNotification(string operationName, string message)
        {
            // TODO: Implement sound notification
            // TODO: Implement system tray notification
        }
        
        private string FormatBytesPerSecond(double bytesPerSecond)
        {
            string[] sizes = { "B/s", "KB/s", "MB/s", "GB/s" };
            int order = 0;
            while (bytesPerSecond >= 1024 && order < sizes.Length - 1)
            {
                order++;
                bytesPerSecond /= 1024;
            }
            return $"{bytesPerSecond:F1} {sizes[order]}";
        }
        
        public static string GenerateOperationId(string prefix)
        {
            return $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
        }
        
        #endregion
        
        #region Public API
        
        public IEnumerable<OperationInfo> GetActiveOperations()
        {
            return _activeOperations.Values.ToList();
        }
        
        public IEnumerable<OperationHistoryItem> GetOperationHistory()
        {
            return _operationHistory.ToList();
        }
        
        public void ClearHistory()
        {
            while (_operationHistory.TryDequeue(out _)) { }
        }
        
        #endregion
        
        public void Dispose()
        {
            _uiUpdateTimer?.Dispose();
            _operationSemaphore?.Dispose();
            
            foreach (var pauseToken in _pauseTokens.Values)
            {
                pauseToken.Cancel();
                pauseToken.Dispose();
            }
            _pauseTokens.Clear();
        }
    }
}
