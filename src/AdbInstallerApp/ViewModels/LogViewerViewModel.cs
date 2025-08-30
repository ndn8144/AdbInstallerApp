using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AdbInstallerApp.Services;

namespace AdbInstallerApp.ViewModels;

public partial class LogViewerViewModel : ObservableObject, IDisposable
{
    private readonly ILogBus _logBus;
    private readonly IDisposable _subscription;
    private bool _disposed;

    [ObservableProperty]
    private ObservableCollection<LogEntry> _logEntries = new();

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    private bool _showDebug = false;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private LogLevel _selectedLogLevel = LogLevel.Info;

    public LogViewerViewModel(ILogBus logBus)
    {
        _logBus = logBus ?? throw new ArgumentNullException(nameof(logBus));
        
        // Subscribe to log stream with filtering
        _subscription = _logBus.Stream
            .Where(entry => ShouldShowEntry(entry))
            .Subscribe(OnLogEntryReceived);

        // Load recent entries
        LoadRecentEntries();
    }

    [RelayCommand]
    private void ClearLogs()
    {
        LogEntries.Clear();
        _logBus.Clear();
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        var text = string.Join(Environment.NewLine, LogEntries.Select(e => e.FormattedMessage));
        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            // Log error but don't crash
            System.Diagnostics.Debug.WriteLine($"Failed to copy to clipboard: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SaveToFile()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Logs",
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"adb_installer_logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                var text = string.Join(Environment.NewLine, LogEntries.Select(e => e.FormattedMessage));
                File.WriteAllText(dialog.FileName, text);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save logs: {ex.Message}");
        }
    }

    [RelayCommand]
    private void RefreshLogs()
    {
        LoadRecentEntries();
    }

    partial void OnFilterTextChanged(string value)
    {
        // Re-filter existing entries
        var filtered = _logBus.GetRecentEntries(1000)
            .Where(entry => ShouldShowEntry(entry))
            .ToList();

        LogEntries.Clear();
        foreach (var entry in filtered)
        {
            LogEntries.Add(entry);
        }
    }

    partial void OnSelectedLogLevelChanged(LogLevel value)
    {
        // Re-filter existing entries
        OnFilterTextChanged(FilterText);
    }

    partial void OnShowDebugChanged(bool value)
    {
        // Re-filter existing entries
        OnFilterTextChanged(FilterText);
    }

    private bool ShouldShowEntry(LogEntry entry)
    {
        // Level filter
        if (entry.Level < SelectedLogLevel) return false;
        
        // Debug filter
        if (entry.Level == LogLevel.Debug && !ShowDebug) return false;
        
        // Text filter
        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            return entry.Message.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
        }
        
        return true;
    }

    private void OnLogEntryReceived(LogEntry entry)
    {
        if (_disposed) return;

        try
        {
            // Add to collection on UI thread
            LogEntries.Add(entry);
            
            // Limit collection size to prevent memory issues
            while (LogEntries.Count > 10000)
            {
                LogEntries.RemoveAt(0);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to add log entry: {ex.Message}");
        }
    }

    private void LoadRecentEntries()
    {
        try
        {
            var recent = _logBus.GetRecentEntries(1000);
            LogEntries.Clear();
            
            foreach (var entry in recent.Where(ShouldShowEntry))
            {
                LogEntries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load recent entries: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _subscription?.Dispose();
    }
}
