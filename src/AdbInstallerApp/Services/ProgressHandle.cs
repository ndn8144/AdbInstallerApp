using System;
using System.Threading;
using AdbInstallerApp.Models;

namespace AdbInstallerApp.Services
{
    /// <summary>
    /// Implementation of progress handle for tracking installation progress
    /// </summary>
    public sealed class ProgressHandle : IProgressHandle, IDisposable
    {
        private readonly CentralizedProgressService _progressService;
        private readonly string _operationId;
        private readonly string _name;
        private readonly double _weight;
        private readonly long _totalBytes;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        private long _completedBytes;
        private bool _isCompleted;
        private bool _isFailed;
        private bool _isCancelled;

        public CancellationToken Token => _cancellationTokenSource.Token;

        public ProgressHandle(CentralizedProgressService progressService, string operationId, string name, 
            double weight = 1.0, long totalBytes = 0)
        {
            _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
            _operationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _weight = weight;
            _totalBytes = totalBytes;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void SetStatus(string status)
        {
            if (_isCompleted || _isFailed || _isCancelled) return;
            
            var progress = _totalBytes > 0 ? (double)_completedBytes / _totalBytes * 100 : 0;
            _progressService.UpdateProgress(_operationId, progress, $"{_name}: {status}", 
                (int)_totalBytes, (int)_completedBytes);
        }

        public void ReportAbsolute(long completedBytes)
        {
            if (_isCompleted || _isFailed || _isCancelled) return;
            
            _completedBytes = Math.Min(completedBytes, _totalBytes);
            var progress = _totalBytes > 0 ? (double)_completedBytes / _totalBytes * 100 : 100;
            _progressService.UpdateProgress(_operationId, progress, _name, 
                (int)_totalBytes, (int)_completedBytes);
        }

        public void Report(long deltaBytes)
        {
            if (_isCompleted || _isFailed || _isCancelled) return;
            
            _completedBytes = Math.Min(_completedBytes + deltaBytes, _totalBytes);
            var progress = _totalBytes > 0 ? (double)_completedBytes / _totalBytes * 100 : 100;
            _progressService.UpdateProgress(_operationId, progress, _name, 
                (int)_totalBytes, (int)_completedBytes);
        }

        public void Complete(string? finalStatus = null)
        {
            if (_isCompleted || _isFailed || _isCancelled) return;
            
            _isCompleted = true;
            _completedBytes = _totalBytes;
            _progressService.UpdateProgress(_operationId, 100, finalStatus ?? $"{_name}: Completed", 
                (int)_totalBytes, (int)_completedBytes);
        }

        public void Fail(string error)
        {
            if (_isCompleted || _isFailed || _isCancelled) return;
            
            _isFailed = true;
            _progressService.UpdateProgress(_operationId, 0, $"{_name}: Failed - {error}", 
                (int)_totalBytes, (int)_completedBytes);
        }

        public void Cancel()
        {
            if (_isCompleted || _isFailed || _isCancelled) return;
            
            _isCancelled = true;
            _cancellationTokenSource.Cancel();
            _progressService.CancelOperation(_operationId);
        }

        public IProgressHandle CreateSlice(string name, double weight, long totalBytes = 0)
        {
            var sliceId = CentralizedProgressService.GenerateOperationId($"{_operationId}-{name}");
            return new ProgressHandle(_progressService, sliceId, $"{_name}/{name}", weight, totalBytes);
        }

        public void Indeterminate(string status)
        {
            if (_isCompleted || _isFailed || _isCancelled) return;
            
            _progressService.UpdateProgress(_operationId, -1, $"{_name}: {status}", 0, 0);
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// Factory for creating progress handles
    /// </summary>
    public sealed class ProgressHandleFactory
    {
        private readonly CentralizedProgressService _progressService;

        public ProgressHandleFactory(CentralizedProgressService progressService)
        {
            _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
        }

        public IProgressHandle CreateHandle(string name, long totalBytes = 0)
        {
            var operationId = CentralizedProgressService.GenerateOperationId(name);
            _progressService.StartOperation(operationId, name, "Starting...");
            return new ProgressHandle(_progressService, operationId, name, 1.0, totalBytes);
        }

        public IProgressHandle CreateSlice(string parentId, string name, double weight, long totalBytes = 0)
        {
            var sliceId = CentralizedProgressService.GenerateOperationId($"{parentId}-{name}");
            return new ProgressHandle(_progressService, sliceId, name, weight, totalBytes);
        }
    }
}
