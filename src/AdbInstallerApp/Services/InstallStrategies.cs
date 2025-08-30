using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdbInstallerApp.Models;

namespace AdbInstallerApp.Services
{
    /// <summary>
    /// Interface for installation strategies
    /// </summary>
    public interface IInstallStrategy
    {
        Task InstallAsync(string serial, InstallationUnit unit, DeviceInstallOptions options, 
            IProgressHandle progress, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Strategy using adb install-multiple command
    /// </summary>
    public sealed class InstallMultipleStrategy : IInstallStrategy
    {
        private readonly AdbService _adbService;

        public InstallMultipleStrategy(AdbService adbService)
        {
            _adbService = adbService ?? throw new ArgumentNullException(nameof(adbService));
        }

        public async Task InstallAsync(string serial, InstallationUnit unit, DeviceInstallOptions options, 
            IProgressHandle progress, CancellationToken cancellationToken)
        {
            progress.SetStatus($"install-multiple {unit.PackageName} ({unit.Files.Count} files)");
            
            try
            {
                // Prepare file paths
                var totalBytes = unit.TotalSize;
                var filePaths = unit.Files.Select(f => f.Path).ToArray();
                
                // Build install options
                var adbOptions = BuildAdbOptions(options);
                
                // Execute install-multiple (atomic at package manager level)
                var success = await _adbService.InstallMultipleAsync(
                    serial, 
                    filePaths, 
                    options.Reinstall, 
                    options.GrantRuntimePermissions, 
                    options.AllowDowngrade, 
                    cancellationToken);

                if (success)
                {
                    progress.ReportAbsolute(totalBytes);
                    progress.Complete($"Installed {unit.PackageName}");
                }
                else
                {
                    progress.Fail($"Failed to install {unit.PackageName}");
                }
            }
            catch (Exception ex)
            {
                progress.Fail($"Install failed: {ex.Message}");
                throw;
            }
        }

        public string BuildAdbOptions(DeviceInstallOptions options)
        {
            var args = new List<string>();
            
            if (options.Reinstall) args.Add("-r");
            if (options.AllowDowngrade) args.Add("-d");
            if (options.GrantRuntimePermissions) args.Add("-g");
            if (options.UserId.HasValue) args.Add($"--user {options.UserId.Value}");
            
            return string.Join(" ", args);
        }
    }

    /// <summary>
    /// Strategy using pm install-create/write/commit session
    /// </summary>
    public sealed class PmSessionStrategy : IInstallStrategy
    {
        private readonly AdbService _adbService;

        public PmSessionStrategy(AdbService adbService)
        {
            _adbService = adbService ?? throw new ArgumentNullException(nameof(adbService));
        }

        public async Task InstallAsync(string serial, InstallationUnit unit, DeviceInstallOptions options, 
            IProgressHandle progress, CancellationToken cancellationToken)
        {
            string? sessionId = null;
            
            try
            {
                // Phase 1: Validate (15% weight)
                using var validateSlice = progress.CreateSlice("Validate", 0.15);
                validateSlice.SetStatus("Validating files...");
                await ValidateFilesAsync(unit, cancellationToken);
                validateSlice.Complete();

                // Phase 2: Create session (10% weight)
                using var sessionSlice = progress.CreateSlice("Session", 0.10);
                sessionSlice.SetStatus("Creating install session...");
                sessionId = await CreateInstallSessionAsync(serial, options, cancellationToken);
                sessionSlice.Complete();

                // Phase 3: Write files (60% weight)
                using var writeSlice = progress.CreateSlice("Write", 0.60, unit.TotalBytes);
                await WriteFilesToSessionAsync(serial, sessionId, unit, options, writeSlice, cancellationToken);

                // Phase 4: Commit session (15% weight)
                using var commitSlice = progress.CreateSlice("Commit", 0.15);
                commitSlice.SetStatus("Committing session...");
                await CommitSessionAsync(serial, sessionId, cancellationToken);
                commitSlice.Complete();

                progress.Complete($"Installed {unit.PackageName}");
            }
            catch (Exception ex)
            {
                // Clean up session on failure
                if (!string.IsNullOrEmpty(sessionId))
                {
                    try
                    {
                        await AbandonSessionAsync(serial, sessionId, CancellationToken.None);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
                
                progress.Fail($"Install failed: {ex.Message}");
                throw;
            }
        }

        private Task ValidateFilesAsync(InstallationUnit unit, CancellationToken cancellationToken)
        {
            foreach (var file in unit.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (!File.Exists(file.Path))
                    throw new FileNotFoundException($"APK file not found: {file.Path}");
                
                var fileInfo = new FileInfo(file.Path);
                if (fileInfo.Length != file.SizeBytes)
                    throw new InvalidOperationException($"File size mismatch for {file.Path}");
            }
            return Task.CompletedTask;
        }

        private async Task<string> CreateInstallSessionAsync(string serial, DeviceInstallOptions options, 
            CancellationToken cancellationToken)
        {
            var pmArgs = BuildPmArgs(options);
            var command = $"shell pm install-create {pmArgs}";
            
            var result = await _adbService.ExecuteAdbCommandAsync(serial, command, null, cancellationToken);
            
            if (!result.Success)
                throw new InvalidOperationException($"Failed to create install session: {result.Output}");
            
            // Extract session ID from output like "Success: created install session [123]"
            var sessionId = ExtractSessionId(result.Output);
            if (string.IsNullOrEmpty(sessionId))
                throw new InvalidOperationException($"Could not extract session ID from: {result.Output}");
            
            return sessionId;
        }

        private async Task WriteFilesToSessionAsync(string serial, string sessionId, InstallationUnit unit, 
            DeviceInstallOptions options, IProgressHandle progress, CancellationToken cancellationToken)
        {
            long totalWritten = 0;
            
            for (int i = 0; i < unit.Files.Count; i++)
            {
                var file = unit.Files[i];
                cancellationToken.ThrowIfCancellationRequested();
                
                progress.SetStatus($"[{i + 1}/{unit.Files.Count}] Writing {Path.GetFileName(file.Path)}");
                
                await WriteFileToSessionAsync(serial, sessionId, file, options, 
                    bytes =>
                    {
                        if (totalWritten < file.SizeBytes)
                            progress.Report(bytes);
                    }, cancellationToken);
            }
        }

        private async Task WriteFileToSessionAsync(string serial, string sessionId, ApkFile file, 
            DeviceInstallOptions options, Action<long> progressCallback, CancellationToken cancellationToken)
        {
            var fileName = Path.GetFileName(file.Path);
            var command = $"shell pm install-write -S {file.SizeBytes} {sessionId} {fileName}";
            
            // Stream file content with progress tracking
            using var fileStream = File.OpenRead(file.Path);
            var buffer = new byte[64 * 1024]; // 64KB chunks
            long totalRead = 0;
            
            // Apply throttling if specified
            var throttleDelay = CalculateThrottleDelay(options.ThrottleMBps, buffer.Length);
            
            while (totalRead < file.SizeBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var chunkSize = (int)Math.Min(buffer.Length, file.SizeBytes - totalRead);
                var bytesRead = await fileStream.ReadAsync(buffer, 0, chunkSize, cancellationToken);
                
                if (bytesRead == 0)
                    break;
                
                // Here we would actually stream to adb, but for now simulate the write
                await Task.Delay(10, cancellationToken); // Simulate write time
                
                totalRead += bytesRead;
                progressCallback(bytesRead);
                
                // Apply throttling
                if (throttleDelay > TimeSpan.Zero)
                    await Task.Delay(throttleDelay, cancellationToken);
            }
        }

        private async Task CommitSessionAsync(string serial, string sessionId, CancellationToken cancellationToken)
        {
            var command = $"shell pm install-commit {sessionId}";
            var result = await _adbService.ExecuteAdbCommandAsync(serial, command, null, cancellationToken);
            
            if (!result.Success)
            {
                // Parse specific error codes for better error handling
                var errorMessage = ParseInstallError(result.Output);
                throw new InvalidOperationException($"Failed to commit session: {errorMessage}");
            }
        }

        private async Task AbandonSessionAsync(string serial, string sessionId, CancellationToken cancellationToken)
        {
            var command = $"shell pm install-abandon {sessionId}";
            await _adbService.ExecuteAdbCommandAsync(serial, command, null, cancellationToken);
        }

        private static string BuildPmArgs(DeviceInstallOptions options)
        {
            var args = new List<string>();
            if (options.Reinstall) args.Add("-r");
            if (options.AllowDowngrade) args.Add("-d");
            if (options.GrantRuntimePermissions) args.Add("-g");
            if (options.UserId is int userId) args.Add($"--user {userId}");
            return string.Join(' ', args);
        }

        private static string? ExtractSessionId(string output)
        {
            // Parse output like "Success: created install session [123]"
            var match = System.Text.RegularExpressions.Regex.Match(output, @"session \[(\d+)\]");
            return match.Success ? match.Groups[1].Value : null;
        }

        public Task<string> ParseInstallError(string output)
        {
            if (string.IsNullOrEmpty(output))
                return Task.FromResult("Unknown installation error");
            
            if (output.Contains("INSTALL_FAILED_ALREADY_EXISTS"))
                return Task.FromResult("Package already exists. Use reinstall option to replace.");
            
            if (output.Contains("INSTALL_FAILED_INSUFFICIENT_STORAGE"))
                return Task.FromResult("Insufficient storage space on device.");
            
            if (output.Contains("INSTALL_FAILED_INVALID_APK"))
                return Task.FromResult("Invalid APK file - check file integrity.");
            
            return Task.FromResult(output);
        }

        public TimeSpan CalculateThrottleDelay(int? throttleMBps, int chunkSize)
        {
            if (throttleMBps == null || throttleMBps <= 0)
                return TimeSpan.Zero;
                
            var bytesPerSecond = throttleMBps.Value * 1024 * 1024;
            var timeForChunk = (double)chunkSize / bytesPerSecond;
            return TimeSpan.FromSeconds(timeForChunk);
        }
    }

    /// <summary>
    /// Factory for creating install strategies
    /// </summary>
    public sealed class InstallStrategyFactory
    {
        private readonly AdbService _adbService;
        private readonly InstallMultipleStrategy _installMultiple;
        private readonly PmSessionStrategy _pmSession;

        public InstallStrategyFactory(AdbService adbService)
        {
            _adbService = adbService ?? throw new ArgumentNullException(nameof(adbService));
            _installMultiple = new InstallMultipleStrategy(adbService);
            _pmSession = new PmSessionStrategy(adbService);
        }

        public IInstallStrategy CreateStrategy(InstallationUnit unit, DeviceInstallOptions options)
        {
            return options.InstallStrategy switch
            {
                InstallStrategy.InstallMultiple => _installMultiple,
                InstallStrategy.PmSession => _pmSession,
                InstallStrategy.Auto => ResolveAutoStrategy(unit, options),
                _ => throw new ArgumentOutOfRangeException(nameof(options.InstallStrategy))
            };
        }

        private IInstallStrategy ResolveAutoStrategy(InstallationUnit unit, DeviceInstallOptions options)
        {
            // Auto-select based on complexity and file characteristics
            var tooManyFiles = unit.Files.Count >= 10;
            var hasLongPaths = unit.Files.Any(f => f.Path.Length > 200 || f.Path.Contains(' '));
            var totalSize = unit.Files.Sum(f => f.SizeBytes);
            var hasLargeFiles = totalSize > 100 * 1024 * 1024; // > 100MB
            
            // Prefer pm session for complex scenarios
            if (tooManyFiles || hasLongPaths || hasLargeFiles)
                return _pmSession;
            
            // Use install-multiple for simple cases
            return _installMultiple;
        }
    }
}
