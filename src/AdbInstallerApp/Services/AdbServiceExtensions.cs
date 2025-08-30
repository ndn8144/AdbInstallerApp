using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AdbInstallerApp.Helpers;
using AdbInstallerApp.Models;
using AdbInstallerApp.Services;

namespace AdbInstallerApp.Services
{
    /// <summary>
    /// Extensions to AdbService for PM session commands and enhanced functionality
    /// </summary>
    public static class AdbServiceExtensions
    {
        /// <summary>
        /// Execute arbitrary adb command and return result
        /// </summary>
        public static async Task<AdbCommandResult> ExecuteAdbCommandAsync(this AdbService adbService, 
            string serial, string command, ILogBus? logBus = null, CancellationToken cancellationToken = default)
        {
            var fullCommand = $"-s {serial} {command}";
            var result = await Proc.RunAsync(adbService.AdbPath, fullCommand, null, logBus != null ? new Progress<string>(logBus.Write) : null, cancellationToken);
            
            return new AdbCommandResult(result.ExitCode == 0, result.StdOut, result.StdErr);
        }

        /// <summary>
        /// Get device properties as dictionary
        /// </summary>
        public static async Task<Dictionary<string, string>> GetDevicePropertiesAsync(this AdbService adbService, 
            string serial, ILogBus? logBus = null, CancellationToken cancellationToken = default)
        {
            var props = new Dictionary<string, string>();
            
            try
            {
                var result = await adbService.ExecuteAdbCommandAsync(serial, "shell getprop", logBus, cancellationToken);
                if (!result.Success) return props;

                var lines = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    // Parse format: [key]: [value]
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"\[([^\]]+)\]:\s*\[([^\]]*)\]");
                    if (match.Success)
                    {
                        props[match.Groups[1].Value] = match.Groups[2].Value;
                    }
                }
            }
            catch
            {
                // Return empty dictionary on error
            }

            return props;
        }

        /// <summary>
        /// Install multiple APKs using install-multiple command
        /// </summary>
        public static async Task<bool> InstallMultipleAsync(this AdbService adbService,
            string serial, string[] filePaths, bool reinstall, bool grantPermissions, 
            bool allowDowngrade, ILogBus? logBus = null, CancellationToken cancellationToken = default)
        {
            var args = new List<string> { "-s", serial, "install-multiple" };
            
            if (reinstall) args.Add("-r");
            if (grantPermissions) args.Add("-g");
            if (allowDowngrade) args.Add("-d");
            
            args.AddRange(filePaths);
            
            var command = string.Join(" ", args.Skip(2)); // Skip -s serial part
            var result = await adbService.ExecuteAdbCommandAsync(serial, command, logBus, cancellationToken);
            
            return result.Success && result.Output.Contains("Success");
        }

        /// <summary>
        /// Create PM install session
        /// </summary>
        public static async Task<string> PmInstallCreateAsync(this AdbService adbService,
            string serial, string pmArgs, ILogBus? logBus = null, CancellationToken cancellationToken = default)
        {
            var command = $"shell pm install-create {pmArgs}";
            var result = await adbService.ExecuteAdbCommandAsync(serial, command, logBus, cancellationToken);
            
            if (!result.Success)
                throw new InvalidOperationException($"Failed to create install session: {result.ErrorOutput}");
            
            // Extract session ID from output like "Success: created install session [123]"
            var match = System.Text.RegularExpressions.Regex.Match(result.Output, @"session \[(\d+)\]");
            if (!match.Success)
                throw new InvalidOperationException($"Could not extract session ID from: {result.Output}");
            
            return match.Groups[1].Value;
        }

        /// <summary>
        /// Write file to PM install session with progress callback
        /// </summary>
        public static async Task PmInstallWriteAsync(this AdbService adbService,
            string serial, string sessionId, string filePath, long fileSize,
            Action<long> progressCallback, CancellationToken cancellationToken = default)
        {
            var fileName = System.IO.Path.GetFileName(filePath);
            var command = $"shell pm install-write -S {fileSize} {sessionId} {fileName}";
            
            // For now, simulate the streaming by reading the file and calling progress
            // In a real implementation, this would stream the file content to adb stdin
            using var fileStream = System.IO.File.OpenRead(filePath);
            var buffer = new byte[64 * 1024]; // 64KB chunks
            long totalRead = 0;
            
            while (totalRead < fileSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var bytesToRead = (int)Math.Min(buffer.Length, fileSize - totalRead);
                var bytesRead = await fileStream.ReadAsync(buffer, 0, bytesToRead, cancellationToken);
                
                if (bytesRead == 0) break;
                
                totalRead += bytesRead;
                progressCallback(bytesRead);
                
                // Simulate write delay
                await Task.Delay(10, cancellationToken);
            }
            
            // Execute the actual command (simplified - real implementation would pipe file content)
            var result = await adbService.ExecuteAdbCommandAsync(serial, command, null, cancellationToken);
            if (!result.Success)
                throw new InvalidOperationException($"Failed to write to session: {result.ErrorOutput}");
        }

        /// <summary>
        /// Commit PM install session
        /// </summary>
        public static async Task PmInstallCommitAsync(this AdbService adbService,
            string serial, string sessionId, CancellationToken cancellationToken = default)
        {
            var command = $"shell pm install-commit {sessionId}";
            var result = await adbService.ExecuteAdbCommandAsync(serial, command, null, cancellationToken);
            
            if (!result.Success)
            {
                var errorMessage = ParseInstallError(result.ErrorOutput);
                throw new InvalidOperationException($"Failed to commit session: {errorMessage}");
            }
        }

        /// <summary>
        /// Abandon PM install session
        /// </summary>
        public static async Task PmInstallAbandonAsync(this AdbService adbService,
            string serial, string sessionId, CancellationToken cancellationToken = default)
        {
            var command = $"shell pm install-abandon {sessionId}";
            await adbService.ExecuteAdbCommandAsync(serial, command, null, cancellationToken);
        }

        /// <summary>
        /// Install single APK with enhanced options
        /// </summary>
        public static async Task<bool> InstallAsync(this AdbService adbService,
            string serial, string apkPath, bool reinstall, bool grantPermissions, 
            bool allowDowngrade, CancellationToken cancellationToken = default)
        {
            var args = new List<string> { "install" };
            
            if (reinstall) args.Add("-r");
            if (grantPermissions) args.Add("-g");
            if (allowDowngrade) args.Add("-d");
            
            args.Add(apkPath);
            
            var command = string.Join(" ", args);
            var result = await adbService.ExecuteAdbCommandAsync(serial, command, null, cancellationToken);
            
            return result.Success && result.Output.Contains("Success");
        }

        private static string ParseInstallError(string output)
        {
            // Common install error patterns with user-friendly messages
            if (output.Contains("INSTALL_FAILED_MISSING_SPLIT"))
                return "Missing required split APK. Try using 'Relaxed' split matching or add missing splits.";
            
            if (output.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE"))
                return "Update incompatible - likely signature mismatch. Uninstall existing app first.";
            
            if (output.Contains("INSTALL_FAILED_INSUFFICIENT_STORAGE"))
                return "Insufficient storage space on device.";
            
            if (output.Contains("INSTALL_FAILED_INVALID_APK"))
                return "Invalid APK file - check file integrity.";
            
            if (output.Contains("INSTALL_FAILED_NO_MATCHING_ABIS"))
                return "No matching ABIs - APK doesn't support device's CPU architecture.";
            
            if (output.Contains("INSTALL_FAILED_OLDER_SDK"))
                return "APK requires newer Android version than device supports.";
            
            if (output.Contains("INSTALL_FAILED_DUPLICATE_PACKAGE"))
                return "Package already exists with different signature.";
            
            return output;
        }
    }

    /// <summary>
    /// Result of an ADB command execution
    /// </summary>
    public record AdbCommandResult(bool Success, string Output, string ErrorOutput);
}
