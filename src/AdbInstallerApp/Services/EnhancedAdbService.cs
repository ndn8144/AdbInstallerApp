using AdbInstallerApp.Models;
using AdbInstallerApp.Utils;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;

namespace AdbInstallerApp.Services
{
    public sealed class AdbInstallOptions
    {
        public bool Reinstall { get; set; }
        public bool Downgrade { get; set; }
        public bool GrantPermissions { get; set; }
        public int? UserId { get; set; }
    }

    public interface IEnhancedAdbService
    {
        Task InstallMultipleAsync(string serial, string[] files, AdbInstallOptions options, CancellationToken ct);
        Task InstallViaSessionAsync(string serial, string[] files, AdbInstallOptions options, IProgress<long>? progress, CancellationToken ct);
        Task<string> CreateInstallSessionAsync(string serial, AdbInstallOptions options, CancellationToken ct);
        Task WriteToSessionAsync(string serial, string sessionId, string filePath, long sizeBytes, IProgress<long>? progress, CancellationToken ct);
        Task CommitSessionAsync(string serial, string sessionId, CancellationToken ct);
    }

    public sealed class EnhancedAdbService : IEnhancedAdbService
    {
        private readonly string _adbPath;
        private readonly ILogBus _logBus;

        public EnhancedAdbService(string adbPath, ILogBus logBus)
        {
            _adbPath = adbPath ?? throw new ArgumentNullException(nameof(adbPath));
            _logBus = logBus ?? throw new ArgumentNullException(nameof(logBus));
        }

        private Task<ProcResult> RunAdbAsync(string args, CancellationToken ct)
        {
            var workingDir = Path.GetDirectoryName(_adbPath);
            var progress = new Progress<string>(_logBus.Write);
            return Proc.RunAsync(_adbPath, args, workingDir, progress, ct);
        }

        public async Task InstallMultipleAsync(string serial, string[] files, AdbInstallOptions options, CancellationToken ct)
        {
            var quotedFiles = string.Join(' ', files.Select(Proc.QuotePath));
            var flags = BuildInstallFlags(options);
            var args = $"-s {serial} install-multiple {flags} {quotedFiles}";
            
            var result = await RunAdbAsync(args, ct).ConfigureAwait(false);
            await EnsureSuccessAsync(result).ConfigureAwait(false);
        }

        public async Task InstallViaSessionAsync(string serial, string[] files, AdbInstallOptions options, IProgress<long>? progress, CancellationToken ct)
        {
            var sessionId = await CreateInstallSessionAsync(serial, options, ct).ConfigureAwait(false);
            
            try
            {
                long totalBytes = 0;
                var fileSizes = new Dictionary<string, long>();
                
                // Calculate total size for progress reporting
                foreach (var file in files)
                {
                    var size = new FileInfo(file).Length;
                    fileSizes[file] = size;
                    totalBytes += size;
                }

                long processedBytes = 0;
                var fileProgress = new Progress<long>(bytes =>
                {
                    processedBytes += bytes;
                    progress?.Report(processedBytes);
                });

                // Write each file to the session
                foreach (var file in files)
                {
                    await WriteToSessionAsync(serial, sessionId, file, fileSizes[file], fileProgress, ct).ConfigureAwait(false);
                }

                // Commit the session
                await CommitSessionAsync(serial, sessionId, ct).ConfigureAwait(false);
            }
            catch
            {
                // Abandon session on error
                try
                {
                    await RunAdbAsync($"-s {serial} shell pm install-abandon {sessionId}", ct).ConfigureAwait(false);
                }
                catch { /* Ignore cleanup errors */ }
                throw;
            }
        }

        public async Task<string> CreateInstallSessionAsync(string serial, AdbInstallOptions options, CancellationToken ct)
        {
            var flags = BuildInstallFlags(options);
            var args = $"-s {serial} shell pm install-create {flags}";
            
            var result = await RunAdbAsync(args, ct).ConfigureAwait(false);
            await EnsureSuccessAsync(result).ConfigureAwait(false);
            
            // Parse session ID from output like "Success: created install session [123456]"
            var match = Regex.Match(result.StdOut, @"\[(\d+)\]");
            if (!match.Success)
                throw new InvalidOperationException($"Cannot parse session ID from: {result.StdOut}");
            
            return match.Groups[1].Value;
        }

        public async Task WriteToSessionAsync(string serial, string sessionId, string filePath, long sizeBytes, IProgress<long>? progress, CancellationToken ct)
        {
            var fileName = Path.GetFileName(filePath);
            var args = $"-s {serial} shell pm install-write -S {sizeBytes} {sessionId} {Proc.QuotePath(fileName)} -";
            
            var psi = new ProcessStartInfo(_adbPath, args)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_adbPath)
            };

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            
            if (!process.Start())
                throw new InvalidOperationException($"Cannot start ADB process for session write");

            using var reg = ct.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch { /* Ignore cleanup errors */ }
            });

            // Stream file content to process stdin
            await using var fileStream = File.OpenRead(filePath);
            var buffer = new byte[256 * 1024]; // 256KB buffer
            long totalWritten = 0;

            try
            {
                while (totalWritten < sizeBytes)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    var bytesToRead = (int)Math.Min(buffer.Length, sizeBytes - totalWritten);
                    var bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, bytesToRead), ct).ConfigureAwait(false);
                    
                    if (bytesRead == 0) break;
                    
                    await process.StandardInput.BaseStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                    totalWritten += bytesRead;
                    progress?.Report(bytesRead);
                }
                
                process.StandardInput.Close();
                await process.WaitForExitAsync(ct).ConfigureAwait(false);
                
                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    throw new InvalidOperationException($"Session write failed: {error}");
                }
            }
            catch
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch { /* Ignore cleanup errors */ }
                throw;
            }
        }

        public async Task CommitSessionAsync(string serial, string sessionId, CancellationToken ct)
        {
            var args = $"-s {serial} shell pm install-commit {sessionId}";
            var result = await RunAdbAsync(args, ct).ConfigureAwait(false);
            await EnsureSuccessAsync(result).ConfigureAwait(false);
        }

        private static string BuildInstallFlags(AdbInstallOptions options)
        {
            var flags = new List<string>();
            
            if (options.Reinstall) flags.Add("-r");
            if (options.Downgrade) flags.Add("-d");
            if (options.GrantPermissions) flags.Add("-g");
            if (options.UserId.HasValue) flags.Add($"--user {options.UserId.Value}");
            
            return string.Join(' ', flags);
        }

        private static Task EnsureSuccessAsync(ProcResult result)
        {
            if (result.ExitCode != 0)
            {
                var error = !string.IsNullOrWhiteSpace(result.StdErr) ? result.StdErr : result.StdOut;
                throw new InvalidOperationException($"ADB command failed (exit code {result.ExitCode}): {error.Trim()}");
            }

            // Check for specific install failures in stdout
            if (result.StdOut.Contains("INSTALL_FAILED"))
            {
                throw new InvalidOperationException($"Installation failed: {result.StdOut.Trim()}");
            }
            
            return Task.CompletedTask;
        }

        public static bool ShouldUseSession(string[] files)
        {
            // Use session for large groups or long command lines
            if (files.Length >= 10) return true;
            
            var totalArgsLength = files.Sum(f => f.Length + 3); // +3 for quotes and space
            return totalArgsLength > 6000;
        }
    }
}
