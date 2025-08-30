using System.Collections.Concurrent;
using System.Reactive.Subjects;
using System.Reactive.Linq;

namespace AdbInstallerApp.Services;

public sealed record StatusEntry(string Message, StatusType Type, DateTime Timestamp, object? Data = null)
{
    public string FormattedMessage => $"[{Timestamp:HH:mm:ss.fff}] {Type.ToString().ToUpperInvariant()}: {Message}";
    public string TimeOnly => Timestamp.ToString("HH:mm:ss.fff");
}

public enum StatusType { Info, Progress, Warning, Error, Success }

public interface IGlobalStatusService : IDisposable
{
    void PushStatus(string message, StatusType type = StatusType.Info, object? data = null);
    void PopStatus();
    void ClearStatus();
    void UpdateProgress(string message, double percentage, object? data = null);
    void SetError(string message, Exception? exception = null);
    void SetSuccess(string message);
    
    IObservable<StatusEntry> StatusStream { get; }
    IObservable<double> ProgressStream { get; }
    
    StatusEntry? CurrentStatus { get; }
    double CurrentProgress { get; }
    bool HasActiveStatus { get; }
    int StatusStackDepth { get; }
}

public sealed class GlobalStatusService : IGlobalStatusService
{
    private readonly ConcurrentStack<StatusEntry> _statusStack = new();
    private readonly Subject<StatusEntry> _statusSubject = new();
    private readonly Subject<double> _progressSubject = new();
    private readonly object _lockObject = new();
    
    private StatusEntry? _currentStatus;
    private double _currentProgress;
    private bool _disposed;

    public IObservable<StatusEntry> StatusStream => _statusSubject.AsObservable();
    public IObservable<double> ProgressStream => _progressSubject.AsObservable();
    
    public StatusEntry? CurrentStatus => _currentStatus;
    public double CurrentProgress => _currentProgress;
    public bool HasActiveStatus => _currentStatus != null;
    public int StatusStackDepth => _statusStack.Count;

    public void PushStatus(string message, StatusType type = StatusType.Info, object? data = null)
    {
        if (_disposed || string.IsNullOrWhiteSpace(message)) return;

        var entry = new StatusEntry(message, type, DateTime.Now, data);
        
        lock (_lockObject)
        {
            _statusStack.Push(entry);
            _currentStatus = entry;
            
            // Reset progress when pushing new status
            if (type != StatusType.Progress)
            {
                _currentProgress = 0.0;
                _progressSubject.OnNext(_currentProgress);
            }
        }
        
        _statusSubject.OnNext(entry);
    }

    public void PopStatus()
    {
        if (_disposed) return;

        lock (_lockObject)
        {
            if (_statusStack.TryPop(out var popped))
            {
                // Update current status to previous entry
                if (_statusStack.TryPeek(out var previous))
                {
                    _currentStatus = previous;
                    _currentProgress = 0.0;
                    _progressSubject.OnNext(_currentProgress);
                }
                else
                {
                    _currentStatus = null;
                    _currentProgress = 0.0;
                    _progressSubject.OnNext(_currentProgress);
                }
                
                // Notify status change
                if (_currentStatus != null)
                {
                    _statusSubject.OnNext(_currentStatus);
                }
            }
        }
    }

    public void ClearStatus()
    {
        if (_disposed) return;

        lock (_lockObject)
        {
            while (_statusStack.TryPop(out _)) { }
            _currentStatus = null;
            _currentProgress = 0.0;
            _progressSubject.OnNext(_currentProgress);
        }
    }

    public void UpdateProgress(string message, double percentage, object? data = null)
    {
        if (_disposed) return;

        var clampedPercentage = Math.Max(0.0, Math.Min(100.0, percentage));
        
        lock (_lockObject)
        {
            _currentProgress = clampedPercentage;
            
            // Update current status if it's a progress type
            if (_currentStatus != null)
            {
                var progressEntry = new StatusEntry(message, StatusType.Progress, DateTime.Now, data);
                _currentStatus = progressEntry;
                _statusSubject.OnNext(progressEntry);
            }
            
            _progressSubject.OnNext(_currentProgress);
        }
    }

    public void SetError(string message, Exception? exception = null)
    {
        if (_disposed) return;

        var data = exception != null ? new { Exception = exception, StackTrace = exception.StackTrace } : null;
        PushStatus(message, StatusType.Error, data);
    }

    public void SetSuccess(string message)
    {
        if (_disposed) return;

        PushStatus(message, StatusType.Success);
    }

    public IDisposable CreateStatusScope(string message, StatusType type = StatusType.Info, object? data = null)
    {
        return new StatusScope(this, message, type, data);
    }

    public IDisposable CreateProgressScope(string message, object? data = null)
    {
        return new ProgressScope(this, message, data);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        ClearStatus();
        _statusSubject.OnCompleted();
        _progressSubject.OnCompleted();
        _statusSubject.Dispose();
        _progressSubject.Dispose();
    }

    private sealed class StatusScope : IDisposable
    {
        private readonly GlobalStatusService _service;
        private bool _disposed;

        public StatusScope(GlobalStatusService service, string message, StatusType type, object? data)
        {
            _service = service;
            _service.PushStatus(message, type, data);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _service.PopStatus();
        }
    }

    private sealed class ProgressScope : IDisposable
    {
        private readonly GlobalStatusService _service;
        private readonly string _message;
        private readonly object? _data;
        private bool _disposed;

        public ProgressScope(GlobalStatusService service, string message, object? data)
        {
            _service = service;
            _message = message;
            _data = data;
            _service.PushStatus(message, StatusType.Progress, data);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _service.PopStatus();
        }
    }
}