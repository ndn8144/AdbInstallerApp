using System.Threading.Channels;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Collections.Concurrent;

namespace AdbInstallerApp.Services;

public sealed record LogEntry(string Message, LogLevel Level, DateTime Timestamp)
{
    public string FormattedMessage => $"[{Timestamp:HH:mm:ss.fff}] [{Level.ToString().ToUpperInvariant()}] {Message}";
    public string TimeOnly => Timestamp.ToString("HH:mm:ss.fff");
}

public enum LogLevel { Info, Warning, Error, Debug }

public interface ILogBus : IDisposable
{
    void Write(string message);
    void WriteWarning(string message);  
    void WriteError(string message);
    void WriteDebug(string message);
    IObservable<LogEntry> Stream { get; }
    void Clear();
    IReadOnlyList<LogEntry> GetRecentEntries(int count = 100);
}

public sealed class LogBus : ILogBus
{
    private readonly Channel<LogEntry> _channel;
    private readonly Subject<LogEntry> _subject = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ConcurrentQueue<LogEntry> _recentEntries = new();
    private readonly Task _processingTask;
    
    // Configuration
    private const int BatchSize = 50;
    private const int BatchIntervalMs = 100;  
    private const int MaxRecentEntries = 1000;

    public IObservable<LogEntry> Stream => _subject.AsObservable();

    public LogBus()
    {
        var options = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };
        
        _channel = Channel.CreateUnbounded<LogEntry>(options);
        _processingTask = ProcessLogEntriesAsync(_cancellationTokenSource.Token);
    }

    public void Write(string message) => WriteLog(message, LogLevel.Info);
    public void WriteWarning(string message) => WriteLog(message, LogLevel.Warning);
    public void WriteError(string message) => WriteLog(message, LogLevel.Error);
    public void WriteDebug(string message) => WriteLog(message, LogLevel.Debug);

    public void Clear()
    {
        while (_recentEntries.TryDequeue(out _)) { }
        // Clear doesn't affect the stream - just recent entries cache
    }

    public IReadOnlyList<LogEntry> GetRecentEntries(int count = 100)
    {
        var entries = new List<LogEntry>();
        var tempQueue = new Queue<LogEntry>();
        
        // Drain queue to temporary storage
        while (_recentEntries.TryDequeue(out var entry))
            tempQueue.Enqueue(entry);
        
        // Take last N entries
        var allEntries = tempQueue.ToArray();
        var startIndex = Math.Max(0, allEntries.Length - count);
        entries.AddRange(allEntries.Skip(startIndex));
        
        // Restore queue
        foreach (var entry in allEntries)
            _recentEntries.Enqueue(entry);
            
        return entries;
    }

    private void WriteLog(string message, LogLevel level)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        
        var entry = new LogEntry(message.Trim(), level, DateTime.Now);
        
        // Add to recent entries cache
        _recentEntries.Enqueue(entry);
        while (_recentEntries.Count > MaxRecentEntries)
            _recentEntries.TryDequeue(out _);
        
        // Send to processing pipeline
        _channel.Writer.TryWrite(entry);
    }

    private async Task ProcessLogEntriesAsync(CancellationToken cancellationToken)
    {
        var batch = new List<LogEntry>(BatchSize);
        
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(BatchIntervalMs));
            
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                // Collect batch
                while (batch.Count < BatchSize && _channel.Reader.TryRead(out var entry))
                    batch.Add(entry);

                if (batch.Count == 0) continue;

                // Publish batch to UI thread
                try
                {
                    foreach (var entry in batch)
                        _subject.OnNext(entry);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LogBus processing error: {ex.Message}");
                }

                batch.Clear();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LogBus background error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _channel.Writer.Complete();
        _cancellationTokenSource.Cancel();
        
        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Ignore shutdown timeout
        }
        
        _subject.OnCompleted();
        _subject.Dispose();
        _cancellationTokenSource.Dispose();
    }
}