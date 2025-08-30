using System.Threading.Channels;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Reactive.Concurrency;

namespace AdbInstallerApp.Services;

/// <summary>
/// Log entry with level and timestamp
/// </summary>
public sealed record LogEntry(string Message, LogLevel Level, DateTime Timestamp)
{
    public string FormattedMessage => $"[{Timestamp:HH:mm:ss.fff}] [{Level.ToString().ToUpperInvariant()}] {Message}";
}

/// <summary>
/// Log levels for categorization
/// </summary>
public enum LogLevel 
{ 
    Info, 
    Warning, 
    Error 
}

/// <summary>
/// Central logging bus interface
/// </summary>
public interface ILogBus : IDisposable
{
    void Write(string line);
    void WriteError(string line);
    void WriteWarning(string line);
    IObservable<LogEntry> Stream { get; }
}

/// <summary>
/// High-performance logging bus with batching and backpressure handling
/// </summary>
public sealed class LogBus : ILogBus
{
    private readonly Channel<LogEntry> _channel;
    private readonly Subject<LogEntry> _subject = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _processingTask;
    
    // Configuration
    private const int BatchSize = 50;
    private const int BatchIntervalMs = 100;

    public IObservable<LogEntry> Stream => _subject.AsObservable();

    public LogBus()
    {
        // Create unbounded channel for high throughput
        _channel = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        _processingTask = ProcessLogEntriesAsync(_cancellationTokenSource.Token);
    }

    public void Write(string line) => WriteLog(line, LogLevel.Info);
    public void WriteError(string line) => WriteLog(line, LogLevel.Error);
    public void WriteWarning(string line) => WriteLog(line, LogLevel.Warning);

    private void WriteLog(string line, LogLevel level)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        
        var entry = new LogEntry(line, level, DateTime.Now);
        
        // Non-blocking write - if channel is closed, ignore
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
                // Read available entries up to batch size
                while (batch.Count < BatchSize && _channel.Reader.TryRead(out var entry))
                {
                    batch.Add(entry);
                }

                if (batch.Count == 0) continue;

                // Publish batch to subscribers
                try
                {
                    foreach (var entry in batch)
                    {
                        _subject.OnNext(entry);
                    }
                }
                catch (Exception ex)
                {
                    // Log processing error - avoid infinite recursion
                    System.Diagnostics.Debug.WriteLine($"LogBus processing error: {ex.Message}");
                }

                batch.Clear();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when disposing
        }
        catch (Exception ex)
        {
            // Unexpected error
            System.Diagnostics.Debug.WriteLine($"LogBus background processing error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // Signal completion and cleanup
        _channel.Writer.Complete();
        _cancellationTokenSource.Cancel();
        
        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Ignore cleanup timeout
        }
        
        _subject.OnCompleted();
        _subject.Dispose();
        _cancellationTokenSource.Dispose();
    }
}