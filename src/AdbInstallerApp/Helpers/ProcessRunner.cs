using System.Diagnostics;
using System.Text;

namespace AdbInstallerApp.Helpers;

/// <summary>
/// Modern process result with detailed information
/// </summary>
public sealed record ProcResult
{
    public int ExitCode { get; init; }
    public string StdOut { get; init; } = "";
    public string StdErr { get; init; } = "";
    public TimeSpan ElapsedTime { get; init; }
    public bool IsSuccess => ExitCode == 0;
}

/// <summary>
/// Replacement for PowerShell-based ProcessRunner with proper async handling
/// </summary>
public static class Proc
{
    /// <summary>
    /// Run process asynchronously with proper cancellation and logging
    /// </summary>
    /// <param name="exe">Executable path</param>
    /// <param name="args">Command line arguments</param>
    /// <param name="workingDir">Working directory (optional)</param>
    /// <param name="log">Progress reporter for real-time output (optional)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Process result</returns>
    public static async Task<ProcResult> RunAsync(
        string exe, 
        string args, 
        string? workingDir = null,
        IProgress<string>? log = null, 
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDir ?? Environment.CurrentDirectory
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdOutBuilder = new StringBuilder();
        var stdErrBuilder = new StringBuilder();

        // Handle output data asynchronously
        process.OutputDataReceived += (_, e) => 
        { 
            if (e.Data is { } line) 
            { 
                stdOutBuilder.AppendLine(line); 
                log?.Report($"[OUT] {line}"); 
            } 
        };
        
        process.ErrorDataReceived += (_, e) => 
        { 
            if (e.Data is { } line) 
            { 
                stdErrBuilder.AppendLine(line); 
                log?.Report($"[ERR] {line}"); 
            } 
        };

        // Start process
        if (!process.Start()) 
        {
            throw new InvalidOperationException($"Cannot start process: {exe} {args}");
        }
            
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Register cancellation callback
        using var registration = ct.Register(() => 
        { 
            try 
            { 
                if (!process.HasExited) 
                {
                    log?.Report($"[CANCEL] Killing process: {exe}");
                    process.Kill(entireProcessTree: true); 
                }
            } 
            catch (Exception ex)
            { 
                log?.Report($"[WARN] Failed to kill process: {ex.Message}");
            } 
        });
        
        // Wait for completion
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        sw.Stop();
        
        return new ProcResult 
        { 
            ExitCode = process.ExitCode, 
            StdOut = stdOutBuilder.ToString(), 
            StdErr = stdErrBuilder.ToString(),
            ElapsedTime = sw.Elapsed
        };
    }

    /// <summary>
    /// Run process and ensure success (throws on non-zero exit code)
    /// </summary>
    public static async Task<ProcResult> RunSuccessAsync(
        string exe, 
        string args, 
        string? workingDir = null,
        IProgress<string>? log = null, 
        CancellationToken ct = default)
    {
        var result = await RunAsync(exe, args, workingDir, log, ct).ConfigureAwait(false);
        
        if (!result.IsSuccess)
        {
            var error = !string.IsNullOrWhiteSpace(result.StdErr) ? result.StdErr : result.StdOut;
            throw new InvalidOperationException($"Process failed with exit code {result.ExitCode}: {error.Trim()}");
        }
        
        return result;
    }
}