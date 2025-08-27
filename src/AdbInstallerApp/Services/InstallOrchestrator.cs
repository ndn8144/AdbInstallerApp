using AdbInstallerApp.Models;
using System.IO;


namespace AdbInstallerApp.Services
{
    public class InstallOrchestrator
    {
        private readonly AdbService _adb;
        public InstallOrchestrator(AdbService adb) { _adb = adb; }


        public async Task InstallAsync(
        IEnumerable<DeviceInfo> devices,
        IEnumerable<ApkItem> apks,
        bool reinstall, bool grant, bool downgrade,
        Action<string>? log = null)
        {
            var apkList = apks.ToList();
            if (apkList.Count == 0) { log?.Invoke("[INFO] No APK selected."); return; }

            var opts = BuildOpts(reinstall, grant, downgrade);

            int i = 0; int n = devices.Count();
            foreach (var d in devices)
            {
                i++;
                log?.Invoke($"[{i}/{n}] Installing on {d.Serial} ({d.State})...");

                if (!string.Equals(d.State, "device", StringComparison.OrdinalIgnoreCase))
                {
                    log?.Invoke($"[SKIP] {d.Serial} is not in 'device' state.");
                    continue;
                }

                // Check architecture compatibility before installation
                var compatibilityIssues = await CheckArchitectureCompatibilityAsync(d, apkList, log);
                if (compatibilityIssues.Any())
                {
                    log?.Invoke($"[WARNING] Architecture compatibility issues detected:");
                    foreach (var issue in compatibilityIssues)
                    {
                        log?.Invoke($"  - {issue}");
                    }
                    log?.Invoke($"[INFO] Proceeding with installation anyway...");
                }

                // Try installation with retry mechanism
                var result = await TryInstallWithRetryAsync(d.Serial, apkList, opts, log);
                
                if (result.ok)
                {
                    log?.Invoke($"[OK] {d.Serial} finished successfully.");
                }
                else
                {
                    log?.Invoke($"[FAIL] {d.Serial} failed.");
                    
                    // Provide additional troubleshooting tips
                    if (result.logText.Contains("INSTALL_FAILED_INVALID_APK"))
                    {
                        log?.Invoke("[TROUBLESHOOTING] APK validation failed. Please check:");
                        log?.Invoke("  - APK file integrity (not corrupted)");
                        log?.Invoke("  - APK compatibility with device");
                        log?.Invoke("  - Try installing APK individually to identify issues");
                    }
                    else if (result.logText.Contains("INSTALL_FAILED_NO_MATCHING_ABIS"))
                    {
                        log?.Invoke("[TROUBLESHOOTING] Architecture mismatch detected. Please check:");
                        log?.Invoke("  - APK supports your device's CPU architecture");
                        log?.Invoke("  - Try finding a universal APK or architecture-specific version");
                        log?.Invoke("  - Use 'Check Architecture Compatibility' tool to verify");
                    }
                }
                
                log?.Invoke("");
            }
        }

        private async Task<(bool ok, string logText)> TryInstallWithRetryAsync(
            string serial, 
            List<ApkItem> apkList, 
            string opts, 
            Action<string>? log)
        {
            const int maxRetries = 2;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                if (attempt > 1)
                {
                    log?.Invoke($"[RETRY] Attempt {attempt}/{maxRetries} for {serial}");
                    
                    // Wait a bit before retry
                    await Task.Delay(2000);
                }

                (bool ok, string logText) result;
                if (apkList.Count == 1)
                {
                    result = await _adb.InstallSingleAsync(serial, apkList[0].FilePath, opts);
                }
                else
                {
                    result = await _adb.InstallMultipleAsync(serial, apkList.Select(a => a.FilePath), opts);
                }

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
                log?.Invoke($"[ATTEMPT {attempt} FAILED] {result.logText}");
                
                // If it's a validation error, don't retry
                if (result.logText.Contains("APK file not found") || 
                    result.logText.Contains("APK file is empty") ||
                    result.logText.Contains("APK file appears to be corrupted"))
                {
                    log?.Invoke("[SKIP RETRY] APK validation failed, skipping retry attempts.");
                    return result;
                }
            }

            return (false, "Max retry attempts exceeded");
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