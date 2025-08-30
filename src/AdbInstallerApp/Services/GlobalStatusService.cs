using CommunityToolkit.Mvvm.ComponentModel;

namespace AdbInstallerApp.Services;

/// <summary>
/// Global status service with stack-based status management
/// </summary>
public sealed partial class GlobalStatusService : ObservableObject
{
    private readonly Stack<string> _statusStack = new();
    
    [ObservableProperty]
    private string _statusText = "Ready";
    
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Push new status onto stack (returns IDisposable for automatic cleanup)
    /// </summary>
    /// <param name="text">Status text to display</param>
    /// <returns>Disposable scope that will pop status when disposed</returns>
    public IDisposable Push(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Status text cannot be empty", nameof(text));
            
        _statusStack.Push(text);
        StatusText = text;
        IsBusy = true;
        
        return new StatusScope(this);
    }

    /// <summary>
    /// Update idle status with system information
    /// </summary>
    /// <param name="onlineDevices">Number of online devices</param>
    /// <param name="groupCount">Number of APK groups</param>
    public void UpdateIdleStatus(int onlineDevices, int groupCount)
    {
        if (_statusStack.Count == 0)
        {
            StatusText = $"Ready — {onlineDevices} devices, {groupCount} groups";
        }
    }

    /// <summary>
    /// Force clear all status (emergency cleanup)
    /// </summary>
    public void ClearAll()
    {
        _statusStack.Clear();
        StatusText = "Ready";
        IsBusy = false;
    }

    private void Pop()
    {
        if (_statusStack.Count > 0)
        {
            _statusStack.Pop();
        }
            
        StatusText = _statusStack.Count > 0 ? _statusStack.Peek() : "Ready";
        IsBusy = _statusStack.Count > 0;
    }

    /// <summary>
    /// Disposable scope for automatic status cleanup
    /// </summary>
    private sealed class StatusScope : IDisposable
    {
        private readonly GlobalStatusService _service;
        private bool _disposed;

        public StatusScope(GlobalStatusService service) 
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            _service.Pop();
        }
    }
}