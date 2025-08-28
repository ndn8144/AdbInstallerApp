using AdbInstallerApp.Models;
using System.IO;


namespace AdbInstallerApp.Services
{
    public class InstallOrchestrator
    {
        private readonly AdbService _adb;
        private readonly CentralizedProgressService _progressService;
        
        public InstallOrchestrator(AdbService adb) 
        { 
            _adb = adb; 
            _progressService = CentralizedProgressService.Instance;
        }


        public async Task InstallAsync(
        IEnumerable<DeviceInfo> devices,
        IEnumerable<ApkItem> apks,
        bool reinstall, bool grant, bool downgrade,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
        {
            var apkList = apks.ToList();
            var deviceList = devices.ToList();
            
            if (apkList.Count == 0) { log?.Invoke("[INFO] No APK selected."); return; }

            // Start progress tracking
            var operationId = CentralizedProgressService.GenerateOperationId("install");
            var totalOperations = deviceList.Count * apkList.Count;
            var completedOperations = 0;
            
            _progressService.StartOperation(operationId, "Installing APKs", "Preparing installation...");

            var opts = BuildOpts(reinstall, grant, downgrade);

            try
            {
                int deviceIndex = 0;
                foreach (var device in deviceList)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    deviceIndex++;
                    log?.Invoke($"[{deviceIndex}/{deviceList.Count}] Installing on {device.Serial} ({device.State})...");

                    if (!string.Equals(device.State, "device", StringComparison.OrdinalIgnoreCase))
                    {
                        log?.Invoke($"[SKIP] {device.Serial} is not in 'device' state.");
                        // Update progress for skipped APKs
                        completedOperations += apkList.Count;
                        var skipProgress = (double)completedOperations / totalOperations * 100;
                        _progressService.UpdateProgress(operationId, skipProgress, 
                            $"Skipped {device.Serial} - device not ready", totalOperations, completedOperations);
                        continue;
                    }

                    // Check architecture compatibility before installation
                    var compatibilityIssues = await CheckArchitectureCompatibilityAsync(device, apkList, log);
                    if (compatibilityIssues.Any())
                    {
                        log?.Invoke($"[WARNING] Architecture compatibility issues detected:");
                        foreach (var issue in compatibilityIssues)
                        {
                            log?.Invoke($"  - {issue}");
                        }
                        log?.Invoke($"[INFO] Proceeding with installation anyway...");
                    }

                    // Install APKs on this device
                    completedOperations = await InstallApksOnDeviceAsync(device, apkList, opts, operationId, 
                        totalOperations, completedOperations, log, cancellationToken);
                    
                    log?.Invoke("");
                }

                _progressService.CompleteOperation(operationId, $"Installation completed - {completedOperations}/{totalOperations} operations");
            }
            catch (OperationCanceledException)
            {
                _progressService.CancelOperation(operationId);
                log?.Invoke("[CANCELLED] Installation was cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                _progressService.CancelOperation(operationId);
                log?.Invoke($"[ERROR] Installation failed: {ex.Message}");
                throw;
            }
        }

        private async Task<int> InstallApksOnDeviceAsync(
            DeviceInfo device, 
            List<ApkItem> apkList, 
            string opts,
            string operationId,
            int totalOperations,
            int completedOperations,
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            for (int apkIndex = 0; apkIndex < apkList.Count; apkIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var apk = apkList[apkIndex];
                var apkFileName = Path.GetFileName(apk.FilePath);
                
                // Update progress before installation
                var progress = (double)completedOperations / totalOperations * 100;
                _progressService.UpdateProgress(operationId, progress, 
                    $"Installing {apkFileName} on {device.Model ?? device.Serial}", 
                    totalOperations, completedOperations);

                log?.Invoke($"  [{apkIndex + 1}/{apkList.Count}] Installing {apkFileName}...");

                // Try installation with retry mechanism
                var result = await TryInstallSingleApkAsync(device.Serial, apk, opts, log, cancellationToken);
                
                completedOperations++;
                
                if (result.ok)
                {
                    log?.Invoke($"    [OK] {apkFileName} installed successfully.");
                }
                else
                {
                    log?.Invoke($"    [FAIL] {apkFileName} failed to install.");
                    
                    // Provide additional troubleshooting tips
                    if (result.logText.Contains("INSTALL_FAILED_INVALID_APK"))
                    {
                        log?.Invoke("    [TROUBLESHOOTING] APK validation failed. Please check:");
                        log?.Invoke("      - APK file integrity (not corrupted)");
                        log?.Invoke("      - APK compatibility with device");
                    }
                    else if (result.logText.Contains("INSTALL_FAILED_NO_MATCHING_ABIS"))
                    {
                        log?.Invoke("    [TROUBLESHOOTING] Architecture mismatch detected.");
                        log?.Invoke("      - APK doesn't support device's CPU architecture");
                    }
                }
            }
            
            return completedOperations;
        }

        private async Task<(bool ok, string logText)> TryInstallSingleApkAsync(
            string serial, 
            ApkItem apk, 
            string opts, 
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            const int maxRetries = 2;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (attempt > 1)
                {
                    log?.Invoke($"    [RETRY] Attempt {attempt}/{maxRetries}");
                    await Task.Delay(2000, cancellationToken);
                }

                var result = await _adb.InstallSingleAsync(serial, apk.FilePath, opts);

                // If successful, return immediately
                if (result.ok)
                {
                    return result;
                }

                // If it's the last attempt, return the failure
                if (attempt == maxRetries)
                {
                    return result;
                }

                // Log the failure and continue to retry
                log?.Invoke($"    [ATTEMPT {attempt} FAILED] {result.log}");
                
                // If it's a validation error, don't retry
                if (result.log.Contains("APK file not found") || 
                    result.log.Contains("APK file is empty") ||
                    result.log.Contains("APK file appears to be corrupted"))
                {
                    log?.Invoke("    [SKIP RETRY] APK validation failed, skipping retry attempts.");
                    return result;
                }
            }

            return (false, "Max retry attempts exceeded");
        }

        /// <summary>
        /// Install multiple APK groups sequentially with progress tracking
        /// </summary>
        public async Task InstallGroupsAsync(
            IEnumerable<DeviceInfo> devices,
            IEnumerable<ApkGroup> groups,
            bool reinstall, bool grant, bool downgrade,
            Action<string>? log = null,
            CancellationToken cancellationToken = default)
        {
            var deviceList = devices.ToList();
            var groupList = groups.ToList();
            
            if (groupList.Count == 0) { log?.Invoke("[INFO] No APK groups selected."); return; }

            // Calculate total operations
            var totalApks = groupList.Sum(g => g.ApkItems.Count);
            var totalOperations = deviceList.Count * totalApks;
            var completedOperations = 0;

            // Start progress tracking
            var operationId = CentralizedProgressService.GenerateOperationId("install-groups");
            _progressService.StartOperation(operationId, "Installing APK Groups", "Preparing group installation...");

            var opts = BuildOpts(reinstall, grant, downgrade);

            try
            {
                log?.Invoke($"[INFO] Starting installation of {groupList.Count} groups on {deviceList.Count} devices");
                log?.Invoke($"[INFO] Total APKs to install: {totalApks}");

                int deviceIndex = 0;
                foreach (var device in deviceList)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    deviceIndex++;
                    log?.Invoke($"\n[DEVICE {deviceIndex}/{deviceList.Count}] {device.Model ?? device.Serial} ({device.State})");

                    if (!string.Equals(device.State, "device", StringComparison.OrdinalIgnoreCase))
                    {
                        log?.Invoke($"[SKIP] Device not ready for installation");
                        // Update progress for all skipped APKs
                        completedOperations += totalApks;
                        var skipProgress = (double)completedOperations / totalOperations * 100;
                        _progressService.UpdateProgress(operationId, skipProgress, 
                            $"Skipped {device.Serial} - device not ready", totalOperations, completedOperations);
                        continue;
                    }

                    // Install each group sequentially
                    int groupIndex = 0;
                    foreach (var group in groupList)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        groupIndex++;
                        log?.Invoke($"  [GROUP {groupIndex}/{groupList.Count}] {group.Name} ({group.ApkItems.Count} APKs)");

                        // Install all APKs in this group
                        completedOperations = await InstallGroupOnDeviceAsync(device, group, opts, operationId, 
                            totalOperations, completedOperations, log, cancellationToken);
                    }
                }

                _progressService.CompleteOperation(operationId, $"Group installation completed - {completedOperations}/{totalOperations} operations");
                log?.Invoke($"\n[COMPLETED] All groups installed successfully!");
            }
            catch (OperationCanceledException)
            {
                _progressService.CancelOperation(operationId);
                log?.Invoke("\n[CANCELLED] Group installation was cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                _progressService.CancelOperation(operationId);
                log?.Invoke($"\n[ERROR] Group installation failed: {ex.Message}");
                throw;
            }
        }

        private async Task<int> InstallGroupOnDeviceAsync(
            DeviceInfo device,
            ApkGroup group,
            string opts,
            string operationId,
            int totalOperations,
            int completedOperations,
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            for (int apkIndex = 0; apkIndex < group.ApkItems.Count; apkIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var apk = group.ApkItems[apkIndex];
                var apkFileName = Path.GetFileName(apk.FilePath);
                
                // Update progress before installation
                var progress = (double)completedOperations / totalOperations * 100;
                _progressService.UpdateProgress(operationId, progress, 
                    $"Installing {apkFileName} from {group.Name}", 
                    totalOperations, completedOperations);

                log?.Invoke($"    [{apkIndex + 1}/{group.ApkItems.Count}] Installing {apkFileName}...");

                // Try installation with retry mechanism
                var result = await TryInstallSingleApkAsync(device.Serial, apk, opts, log, cancellationToken);
                
                completedOperations++;
                
                if (result.ok)
                {
                    log?.Invoke($"      [OK] {apkFileName} installed successfully.");
                }
                else
                {
                    log?.Invoke($"      [FAIL] {apkFileName} failed to install.");
                    log?.Invoke($"      [ERROR] {result.logText}");
                }
            }
            
            return completedOperations;
        }

        private static string BuildOpts(bool reinstall, bool grant, bool downgrade)
        {
            var list = new List<string>();
            if (reinstall) list.Add("-r");
            if (grant) list.Add("-g");
            if (downgrade) list.Add("-d");
            return string.Join(' ', list);
        }

        private async Task<List<string>> CheckArchitectureCompatibilityAsync(DeviceInfo device, List<ApkItem> apks, Action<string>? log)
        {
            var issues = new List<string>();
            var deviceArch = device.Abi?.ToLower();
            
            if (string.IsNullOrEmpty(deviceArch))
            {
                issues.Add("Device architecture unknown - cannot verify compatibility");
                return issues;
            }

            var validationService = new ApkValidationService();
            
            foreach (var apk in apks)
            {
                try
                {
                    var validationResult = await validationService.ValidateApkAsync(apk.FilePath);
                    
                    if (validationResult.IsValid && validationResult.ApkInfo != null)
                    {
                        var info = validationResult.ApkInfo;
                        
                        if (info.HasNativeLibraries && info.SupportedArchitectures.Count > 0)
                        {
                            var isCompatible = CheckArchitectureCompatibility(deviceArch, info.SupportedArchitectures);
                            if (!isCompatible)
                            {
                                var fileName = Path.GetFileName(apk.FilePath);
                                issues.Add($"{fileName}: Device {deviceArch} not supported (APK supports: {string.Join(", ", info.SupportedArchitectures)})");
                            }
                        }
                    }
                }
                catch
                {
                    // Skip validation errors, continue with installation
                }
            }
            
            return issues;
        }

        private static bool CheckArchitectureCompatibility(string deviceArch, List<string> supportedArchs)
        {
            // Map device architectures to APK architectures
            var archMapping = new Dictionary<string, string[]>
            {
                ["arm64-v8a"] = new[] { "arm64-v8a" },
                ["armeabi-v7a"] = new[] { "armeabi-v7a", "armeabi" },
                ["armeabi"] = new[] { "armeabi" },
                ["x86_64"] = new[] { "x86_64", "x86" },
                ["x86"] = new[] { "x86" },
                ["mips64"] = new[] { "mips64", "mips" },
                ["mips"] = new[] { "mips" }
            };

            if (archMapping.TryGetValue(deviceArch, out var compatibleArchs))
            {
                return supportedArchs.Any(apkArch => compatibleArchs.Contains(apkArch));
            }

            return false;
        }
    }
}