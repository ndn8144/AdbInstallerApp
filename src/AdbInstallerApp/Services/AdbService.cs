using AdbInstallerApp.Helpers;
using AdbInstallerApp.Models;
using System.Text.RegularExpressions;
using System.IO;
using AdbInstallerApp.Services; // Added for ApkValidationService


namespace AdbInstallerApp.Services
{
    public class AdbService
    {
        private readonly string _adbPath;
        private readonly ILogBus? _logBus; // Make nullable for backward compatibility

        public AdbService(ILogBus? logBus = null)
        {
            _logBus = logBus;
            // Prefer bundled adb under solution root / tools/platform-tools
            var baseDir = AppContext.BaseDirectory;
            var toolsCandidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "tools", "platform-tools", "adb.exe"));
            
            System.Diagnostics.Debug.WriteLine($"AdbService: BaseDirectory = {baseDir}");
            System.Diagnostics.Debug.WriteLine($"AdbService: Checking bundled ADB at: {toolsCandidate}");
            System.Diagnostics.Debug.WriteLine($"AdbService: Bundled ADB exists: {File.Exists(toolsCandidate)}");
            
            if (File.Exists(toolsCandidate)) 
            {
                _adbPath = toolsCandidate;
                System.Diagnostics.Debug.WriteLine($"AdbService: Using bundled ADB: {_adbPath}");
            }
            else 
            {
                _adbPath = "adb"; // fallback to PATH
                System.Diagnostics.Debug.WriteLine($"AdbService: Using system PATH ADB: {_adbPath}");
            }
        }

        public string AdbPath => _adbPath;

        public async Task StartServerAsync()
        {
            await Proc.RunAsync(_adbPath, "start-server", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
        }


        public async Task<List<DeviceInfo>> ListDevicesAsync()
        {
            var list = new List<DeviceInfo>();
            var result = await Proc.RunAsync(_adbPath, "devices", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var code = result.ExitCode;
            var stdout = result.StdOut;
            
            if (code != 0) 
            {
                System.Diagnostics.Debug.WriteLine($"ADB ERROR: {result.StdErr}");
                return list;
            }

            System.Diagnostics.Debug.WriteLine($"RAW ADB OUTPUT:\n'{stdout}'");
            System.Diagnostics.Debug.WriteLine($"RAW OUTPUT LENGTH: {stdout.Length}");
            var lines = stdout.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            System.Diagnostics.Debug.WriteLine($"TOTAL LINES AFTER SPLIT: {lines.Length}");
            
            for (int i = 0; i < lines.Length; i++)
            {
                System.Diagnostics.Debug.WriteLine($"LINE {i}: '{lines[i]}'");
            }
            
            foreach (var line in lines.Skip(1)) // skip header
            {
                // Skip empty lines and lines with only whitespace
                if (string.IsNullOrWhiteSpace(line)) 
                {
                    System.Diagnostics.Debug.WriteLine($"SKIPPING EMPTY/WHITESPACE LINE: '{line}'");
                    continue;
                }
                
                // Skip lines that are just whitespace or newlines
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine))
                {
                    System.Diagnostics.Debug.WriteLine($"SKIPPING TRIMMED EMPTY LINE: '{line}'");
                    continue;
                }
                
                System.Diagnostics.Debug.WriteLine($"PROCESSING LINE: '{trimmedLine}' (Original: '{line}')");
                
                // Parse using regex to handle multiple whitespace
                var parts = Regex.Split(trimmedLine, @"\s+");
                System.Diagnostics.Debug.WriteLine($"REGEX SPLIT PARTS: {parts.Length}");
                for (int i = 0; i < parts.Length; i++)
                {
                    System.Diagnostics.Debug.WriteLine($"  Part[{i}]: '{parts[i]}'");
                }
                
                if (parts.Length < 2)
                {
                    System.Diagnostics.Debug.WriteLine($"SKIPPING - NOT ENOUGH PARTS: {parts.Length}");
                    continue;
                }
                
                string serial = parts[0].Trim();
                string state = parts[1].Trim();
                
                if (string.IsNullOrWhiteSpace(serial))
                {
                    System.Diagnostics.Debug.WriteLine($"SKIPPING - EMPTY SERIAL");
                    continue;
                }
                
                System.Diagnostics.Debug.WriteLine($"PARSED DEVICE: Serial='{serial}', State='{state}'");
                
                var device = new DeviceInfo { Serial = serial, State = state };
                list.Add(device);
                System.Diagnostics.Debug.WriteLine($"ADDED TO LIST: {serial} - {state} (Total: {list.Count})");
                
                // Only enrich authorized devices to avoid blocking
                if (device.State == "device")
                {
                    try
                    {
                        await EnrichDeviceInfoAsync(device);
                        System.Diagnostics.Debug.WriteLine($"ENRICHED: {serial}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ENRICHMENT FAILED: {serial} - {ex.Message}");
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"FINAL DEVICE COUNT: {list.Count}");
            return list;
        }

        private async Task EnrichDeviceInfoAsync(DeviceInfo device)
        {
            try
            {
                // Get device properties
                await GetDevicePropertiesAsync(device);

                // Get hardware information
                await GetHardwareInfoAsync(device);

                // Get network information
                await GetNetworkInfoAsync(device);

                // Get display information
                await GetDisplayInfoAsync(device);

                // Get security information
                await GetSecurityInfoAsync(device);

                // Get runtime information
                await GetRuntimeInfoAsync(device);

                // Get temperature and sensor information
                await GetSensorInfoAsync(device);

                // Get developer options status
                await GetDeveloperOptionsStatusAsync(device);

                // Perform troubleshooting checks
                PerformTroubleshootingChecksAsync(device);
            }
            catch
            {
                // Ignore errors, use default values
            }
        }

        private async Task GetDevicePropertiesAsync(DeviceInfo device)
        {
            var result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell getprop", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var code = result.ExitCode;
            var stdout = result.StdOut;
            if (code == 0)
            {
                var lines = stdout.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains(": "))
                    {
                        var parts = line.Split(new[] { ": " }, 2, StringSplitOptions.None);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim().Trim('[', ']');

                            switch (key)
                            {
                                case "[ro.product.manufacturer]":
                                    device.Manufacturer = value;
                                    break;
                                case "[ro.product.model]":
                                    device.Model = value;
                                    break;
                                case "[ro.product.name]":
                                    device.Product = value;
                                    break;
                                case "[ro.build.version.release]":
                                    device.AndroidVersion = value;
                                    break;
                                case "[ro.build.version.sdk]":
                                    device.Sdk = value;
                                    break;
                                case "[ro.product.cpu.abi]":
                                    device.Abi = value;
                                    break;
                                case "[ro.sf.lcd_density]":
                                    device.Density = value;
                                    device.LcdDensity = value;
                                    break;
                                case "[ro.build.display.id]":
                                    device.BuildNumber = value;
                                    break;

                                // System Build Information
                                case "[ro.build.fingerprint]":
                                    device.BuildFingerprint = value;
                                    break;
                                case "[ro.build.date]":
                                    device.BuildDate = value;
                                    break;
                                case "[ro.build.user]":
                                    device.BuildUser = value;
                                    break;
                                case "[ro.build.host]":
                                    device.BuildHost = value;
                                    break;
                                case "[ro.build.tags]":
                                    device.BuildTags = value;
                                    break;
                                case "[ro.debuggable]":
                                    device.IsDebuggable = value == "1";
                                    break;
                                case "[ro.secure]":
                                    device.IsSecure = value == "1";
                                    break;
                                case "[ro.build.type]":
                                    device.BuildType = value;
                                    break;

                                // Hardware & Performance
                                case "[ro.product.cpu.abilist]":
                                    device.SupportedAbis = value;
                                    break;
                                case "[ro.product.cpu.abilist32]":
                                    device.SupportedAbis32 = value;
                                    break;
                                case "[ro.product.cpu.abilist64]":
                                    device.SupportedAbis64 = value;
                                    break;

                                // Display & Graphics
                                case "[debug.egl.hw]":
                                    device.HasHardwareAcceleration = value == "1";
                                    break;

                                // Security & Permissions
                                case "[ro.crypto.state]":
                                    device.EncryptionState = value;
                                    break;
                                case "[vold.decrypt]":
                                    device.DecryptionStatus = value;
                                    break;
                            }
                        }
                    }
                }
            }
        }

        private async Task GetHardwareInfoAsync(DeviceInfo device)
        {
            // CPU Information
            var result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell cat /proc/cpuinfo", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var cpuCode = result.ExitCode;
            var cpuOutput = result.StdOut;
            if (cpuCode == 0)
            {
                device.CpuInfo = cpuOutput;
            }

            // Memory Information
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell cat /proc/meminfo", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var memCode = result.ExitCode;
            var memOutput = result.StdOut;
            if (memCode == 0)
            {
                device.MemoryInfo = memOutput;
            }

            // Storage Information
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell df", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var storageCode = result.ExitCode;
            var storageOutput = result.StdOut;
            if (storageCode == 0)
            {
                device.StorageInfo = storageOutput;
            }

            // Partition Information
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell cat /proc/partitions", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var partCode = result.ExitCode;
            var partOutput = result.StdOut;
            if (partCode == 0)
            {
                device.PartitionInfo = partOutput;
            }

            // Battery Information
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell dumpsys battery", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var batteryCode = result.ExitCode;
            var batteryOutput = result.StdOut;
            if (batteryCode == 0)
            {
                device.BatteryStatus = batteryOutput;

                // Extract battery level
                var levelMatch = Regex.Match(batteryOutput, @"level:\s*(\d+)");
                if (levelMatch.Success)
                {
                    device.BatteryLevel = levelMatch.Groups[1].Value;
                }

                // Extract battery temperature
                var tempMatch = Regex.Match(batteryOutput, @"temperature:\s*(\d+)");
                if (tempMatch.Success)
                {
                    var tempC = int.Parse(tempMatch.Groups[1].Value) / 10.0;
                    device.BatteryTemperature = $"{tempC:F1}";
                }

                // Check if charging
                device.IsCharging = batteryOutput.Contains("status: 2") || batteryOutput.Contains("AC powered: true");
            }

            // Battery capacity
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell cat /sys/class/power_supply/battery/capacity", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var capacityCode = result.ExitCode;
            var capacityOutput = result.StdOut;
            if (capacityCode == 0 && string.IsNullOrEmpty(device.BatteryLevel))
            {
                device.BatteryLevel = capacityOutput.Trim();
            }
        }

        private async Task GetNetworkInfoAsync(DeviceInfo device)
        {
            // Network Interfaces
            var result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell ip addr show", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var netCode = result.ExitCode;
            var netOutput = result.StdOut;
            if (netCode == 0)
            {
                device.NetworkInterfaces = netOutput;
            }

            // WiFi Information
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell dumpsys wifi", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var wifiCode = result.ExitCode;
            var wifiOutput = result.StdOut;
            if (wifiCode == 0)
            {
                device.WifiInfo = wifiOutput;

                // Extract WiFi SSID
                var ssidMatch = Regex.Match(wifiOutput, @"SSID:\s*""([^""]*)""");
                if (ssidMatch.Success)
                {
                    device.WifiSsid = ssidMatch.Groups[1].Value;
                    device.IsWifiConnected = true;
                }
            }

            // Connectivity Information
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell dumpsys connectivity", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var connCode = result.ExitCode;
            var connOutput = result.StdOut;
            if (connCode == 0)
            {
                device.ConnectivityInfo = connOutput;
                device.HasMobileData = connOutput.Contains("mobile") || connOutput.Contains("cellular");
            }

            // Network Statistics
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell cat /proc/net/dev", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var statsCode = result.ExitCode;
            var statsOutput = result.StdOut;
            if (statsCode == 0)
            {
                device.NetworkStats = statsOutput;
            }
        }

        private async Task GetDisplayInfoAsync(DeviceInfo device)
        {
            // Screen Resolution
            var result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell wm size", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var sizeCode = result.ExitCode;
            var sizeOutput = result.StdOut;
            if (sizeCode == 0)
            {
                device.ScreenResolution = sizeOutput.Trim();
            }

            // Screen Density
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell wm density", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var densityCode = result.ExitCode;
            var densityOutput = result.StdOut;
            if (densityCode == 0)
            {
                device.ScreenDensity = densityOutput.Trim();
            }

            // Display Information
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell dumpsys display", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var displayCode = result.ExitCode;
            var displayOutput = result.StdOut;
            if (displayCode == 0)
            {
                device.DisplayInfo = displayOutput;
            }

            // Graphics Information
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell dumpsys SurfaceFlinger", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var graphicsCode = result.ExitCode;
            var graphicsOutput = result.StdOut;
            if (graphicsCode == 0)
            {
                device.GraphicsInfo = graphicsOutput;
            }
        }

        private async Task GetSecurityInfoAsync(DeviceInfo device)
        {
            // SELinux Mode
            var result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell getenforce", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var selinuxCode = result.ExitCode;
            var selinuxOutput = result.StdOut;
            if (selinuxCode == 0)
            {
                device.SelinuxMode = selinuxOutput.Trim();
            }

            // Kernel Version
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell cat /proc/version", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var kernelCode = result.ExitCode;
            var kernelOutput = result.StdOut;
            if (kernelCode == 0)
            {
                device.KernelVersion = kernelOutput.Trim();
            }

            // Check if device is rooted
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell which su", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var rootCode = result.ExitCode;
            var rootOutput = result.StdOut;
            device.IsRooted = rootCode == 0 && !string.IsNullOrWhiteSpace(rootOutput.Trim());
            if (device.IsRooted)
            {
                device.SuBinaryLocation = rootOutput.Trim();
            }

            // Check for Superuser app
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell ls /system/app/Superuser.apk", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var superuserCode = result.ExitCode;
            var superuserOutput = result.StdOut;
            device.HasSuperuserApp = superuserCode == 0 && !string.IsNullOrWhiteSpace(superuserOutput.Trim());

            // Check for xbin tools
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell ls /system/xbin/which", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var xbinCode = result.ExitCode;
            var xbinOutput = result.StdOut;
            device.HasXbinTools = xbinCode == 0 && !string.IsNullOrWhiteSpace(xbinOutput.Trim());
        }

        private async Task GetRuntimeInfoAsync(DeviceInfo device)
        {
            // Package Information
            var result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell pm list packages", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var packagesCode = result.ExitCode;
            var packagesOutput = result.StdOut;
            if (packagesCode == 0)
            {
                var packages = packagesOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                device.TotalPackages = packages.Length;
            }

            // Third-party packages
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell pm list packages -3", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var thirdPartyCode = result.ExitCode;
            var thirdPartyOutput = result.StdOut;
            if (thirdPartyCode == 0)
            {
                var thirdPartyPackages = thirdPartyOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                device.ThirdPartyPackages = thirdPartyPackages.Length;
            }

            // System packages
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell pm list packages -s", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var systemCode = result.ExitCode;
            var systemOutput = result.StdOut;
            if (systemCode == 0)
            {
                var systemPackages = systemOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                device.SystemPackages = systemPackages.Length;
            }

            // Disabled packages
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell pm list packages -d", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var disabledCode = result.ExitCode;
            var disabledOutput = result.StdOut;
            if (disabledCode == 0)
            {
                var disabledPackages = disabledOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                device.DisabledPackages = disabledPackages.Length;
            }

            // Process Information
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell ps", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var processCode = result.ExitCode;
            var processOutput = result.StdOut;
            if (processCode == 0)
            {
                device.ProcessInfo = processOutput;
            }

            // CPU Usage
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell top -n 1", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var cpuUsageCode = result.ExitCode;
            var cpuUsageOutput = result.StdOut;
            if (cpuUsageCode == 0)
            {
                device.CpuUsage = cpuUsageOutput;
            }

            // Activity Manager Information
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell dumpsys activity", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var activityCode = result.ExitCode;
            var activityOutput = result.StdOut;
            if (activityCode == 0)
            {
                device.ActivityManagerInfo = activityOutput;
            }
        }

        private async Task GetSensorInfoAsync(DeviceInfo device)
        {
            // Battery Temperature (alternative method)
            if (string.IsNullOrEmpty(device.BatteryTemperature))
            {
                var result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell dumpsys battery | grep temperature", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
                var tempCode = result.ExitCode;
                var tempOutput = result.StdOut;
                if (tempCode == 0)
                {
                    var tempMatch = Regex.Match(tempOutput, @"temperature:\s*(\d+)");
                    if (tempMatch.Success)
                    {
                        var tempC = int.Parse(tempMatch.Groups[1].Value) / 10.0;
                        device.BatteryTemperature = $"{tempC:F1}";
                    }
                }
            }

            // Thermal Zones
            var thermalResult = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell cat /sys/class/thermal/thermal_zone*/temp", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var thermalCode = thermalResult.ExitCode;
            var thermalOutput = thermalResult.StdOut;
            if (thermalCode == 0)
            {
                device.ThermalZones = thermalOutput;
            }

            // Sensor Information
            var sensorResult = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell dumpsys sensorservice", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var sensorCode = sensorResult.ExitCode;
            var sensorOutput = sensorResult.StdOut;
            if (sensorCode == 0)
            {
                device.SensorInfo = sensorOutput;
            }
        }

        private async Task GetDeveloperOptionsStatusAsync(DeviceInfo device)
        {
            // Developer Options Enabled
            var result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell settings get global development_settings_enabled", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var devOptsCode = result.ExitCode;
            var devOptsOutput = result.StdOut;
            if (devOptsCode == 0)
            {
                device.DeveloperOptionsEnabled = devOptsOutput.Trim() == "1";
            }

            // ADB Enabled
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell settings get global adb_enabled", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var adbCode = result.ExitCode;
            var adbOutput = result.StdOut;
            if (adbCode == 0)
            {
                device.AdbEnabled = adbOutput.Trim() == "1";
            }

            // USB Debugging Enabled
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell settings get global usb_debugging_enabled", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var usbDebugCode = result.ExitCode;
            var usbDebugOutput = result.StdOut;
            if (usbDebugCode == 0)
            {
                device.UsbDebuggingEnabled = usbDebugOutput.Trim() == "1";
            }

            // Verify Apps Over USB
            result = await Proc.RunAsync(_adbPath, $"-s {device.Serial} shell settings get global verify_apps_over_usb", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var verifyCode = result.ExitCode;
            var verifyOutput = result.StdOut;
            if (verifyCode == 0)
            {
                device.VerifyAppsOverUsb = verifyOutput.Trim() == "1";
            }
        }

        private void PerformTroubleshootingChecksAsync(DeviceInfo device)
        {
            // USB Connection Check
            device.UsbConnectionOk = device.State == "device";

            // Driver Installation Check (basic check)
            device.DriverInstalled = device.State != "offline";

            // ADB Authorization Check
            device.AdbAuthorized = device.State == "device";

            // Device Compatibility Check
            device.DeviceCompatible = !string.IsNullOrEmpty(device.Abi) &&
                                    !string.IsNullOrEmpty(device.AndroidVersion) &&
                                    !string.IsNullOrEmpty(device.Sdk);

            // Connection Diagnosis
            var diagnosis = new List<string>();

            if (!device.UsbConnectionOk)
            {
                diagnosis.Add("USB connection failed");
            }

            if (!device.DriverInstalled)
            {
                diagnosis.Add("Driver not installed");
            }

            if (!device.DeviceCompatible)
            {
                diagnosis.Add("Device compatibility issues");
            }

            if (diagnosis.Count == 0)
            {
                device.ConnectionDiagnosis = "All checks passed";
            }
            else
            {
                device.ConnectionDiagnosis = string.Join("; ", diagnosis);
            }
        }

        public async Task<(bool ok, string log)> InstallSingleAsync(string serial, string apkPath, string opts)
        {
            // Validate APK file before installation
            if (!File.Exists(apkPath))
            {
                return (false, $"APK file not found: {apkPath}");
            }

            var fileInfo = new FileInfo(apkPath);
            if (fileInfo.Length == 0)
            {
                return (false, $"APK file is empty: {apkPath}");
            }

            // Use ApkValidationService for comprehensive validation
            var validationService = new ApkValidationService();
            var validationResult = await validationService.ValidateApkAsync(apkPath);
            
            if (!validationResult.IsValid)
            {
                var errorLog = $"[VALIDATION FAILED] {validationResult.ErrorMessage}\n";
                if (validationResult.Warnings.Any())
                {
                    errorLog += "[WARNINGS]\n" + string.Join("\n", validationResult.Warnings.Select(w => $"  - {w}"));
                }
                return (false, errorLog);
            }

            // Log warnings if any
            if (validationResult.Warnings.Any())
            {
                var warningLog = "[WARNINGS] APK has some issues but may still install:\n";
                warningLog += string.Join("\n", validationResult.Warnings.Select(w => $"  - {w}"));
                // Continue with installation despite warnings
            }

            var args = $"-s {serial} install {opts} \"{apkPath}\"";
            var result = await Proc.RunAsync(_adbPath, args, null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var code = result.ExitCode;
            var so = result.StdOut;
            var se = result.StdErr;
            
            // Enhanced error handling
            var log = so + se;
            if (code != 0)
            {
                log = $"[ERROR] Installation failed with exit code {code}\n{log}";
                
                // Provide specific error messages for common issues
                if (log.Contains("INSTALL_FAILED_INVALID_APK"))
                {
                    log += "\n\n[SOLUTION] This APK file appears to be corrupted or invalid. Please:\n" +
                           "1. Verify the APK file is not corrupted\n" +
                           "2. Try downloading the APK again\n" +
                           "3. Check if the APK is compatible with your device\n" +
                           "4. Consider using the APK repair tool if available";
                }
                else if (log.Contains("INSTALL_FAILED_NO_MATCHING_ABIS"))
                {
                    log += "\n\n[SOLUTION] APK architecture mismatch. Please:\n" +
                           "1. Check if APK supports your device's CPU architecture\n" +
                           "2. Try finding a universal APK (supports multiple architectures)\n" +
                           "3. Verify device architecture: ARM, ARM64, x86, or x86_64\n" +
                           "4. Some APKs may need to be downloaded specifically for your device type";
                }
                else if (log.Contains("INSTALL_FAILED_OLDER_SDK"))
                {
                    log += "\n\n[SOLUTION] This APK requires a newer Android version. Please:\n" +
                           "1. Update your device to a newer Android version\n" +
                           "2. Or find an APK compatible with your current Android version";
                }
                else if (log.Contains("INSTALL_FAILED_USER_RESTRICTED"))
                {
                    log += "\n\n[SOLUTION] Installation blocked by device settings. Please:\n" +
                           "1. Enable 'Install from unknown sources' in device settings\n" +
                           "2. Disable 'Verify apps over USB' in Developer Options";
                }
            }
            
            return (code == 0, log);
        }

        public async Task<(bool ok, string log)> InstallMultipleAsync(string serial, IEnumerable<string> apkPaths, string opts)
        {
            var apkList = apkPaths.ToList();
            
            System.Diagnostics.Debug.WriteLine($"InstallMultipleAsync: Installing {apkList.Count} APKs to {serial}");
            foreach (var path in apkList)
            {
                System.Diagnostics.Debug.WriteLine($"  APK: {path}");
            }
            
            // Basic file existence check only - skip validation for install-multiple
            foreach (var apkPath in apkList)
            {
                if (!File.Exists(apkPath))
                {
                    return (false, $"APK file not found: {apkPath}");
                }

                var fileInfo = new FileInfo(apkPath);
                if (fileInfo.Length == 0)
                {
                    return (false, $"APK file is empty: {apkPath}");
                }
            }

            var joined = string.Join(" ", apkList.Select(p => $"\"{p}\""));
            var args = $"-s {serial} install-multiple {opts} {joined}";
            
            System.Diagnostics.Debug.WriteLine($"Running command: adb {args}");
            var result = await Proc.RunAsync(_adbPath, args, null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
            var code = result.ExitCode;
            var so = result.StdOut;
            var se = result.StdErr;
            
            System.Diagnostics.Debug.WriteLine($"Install result: Code={code}, Output='{so}', Error='{se}'");
            
            var log = so + se;
            if (code != 0)
            {
                log = $"[ERROR] Multiple APK installation failed with exit code {code}\n{log}";
                
                if (log.Contains("INSTALL_FAILED_INVALID_APK"))
                {
                    log += "\n\n[SOLUTION] One or more APK files appear to be corrupted. Please:\n" +
                           "1. Verify all APK files are not corrupted\n" +
                           "2. Try downloading the APKs again\n" +
                           "3. Check if the APKs are compatible with your device\n" +
                           "4. Consider installing APKs one by one to identify the problematic one\n" +
                           "5. Consider using the APK repair tool if available";
                }
            }
            else
            {
                log = $"[SUCCESS] Multiple APK installation completed\n{log}";
            }
            
            return (code == 0, log);
        }

        /// <summary>
        /// Get device properties for split APK matching
        /// </summary>
        public async Task<Dictionary<string, string>> GetDevicePropertiesAsync(string serial)
        {
            var props = new Dictionary<string, string>();
            
            try
            {
                var result = await Proc.RunAsync(_adbPath, $"-s {serial} shell getprop", null, _logBus != null ? new Progress<string>(_logBus.Write) : null);
                var code = result.ExitCode;
                var output = result.StdOut;
                
                if (code == 0)
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var match = Regex.Match(line, @"\[(.*?)\]:\s*\[(.*?)\]");
                        if (match.Success)
                        {
                            var key = match.Groups[1].Value;
                            var value = match.Groups[2].Value;
                            props[key] = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting device properties for {serial}: {ex.Message}");
            }
            
            return props;
        }

        /// <summary>
        /// Install single APK with enhanced options
        /// </summary>
        public async Task<bool> InstallAsync(string serial, string apkPath, bool reinstall, bool grantPerms, bool allowDowngrade, CancellationToken cancellationToken = default)
        {
            var options = new List<string>();
            if (reinstall) options.Add("-r");
            if (grantPerms) options.Add("-g");
            if (allowDowngrade) options.Add("-d");
            
            var opts = string.Join(" ", options);
            var (success, _) = await InstallSingleAsync(serial, apkPath, opts);
            return success;
        }

        /// <summary>
        /// Install multiple APKs with enhanced options
        /// </summary>
        public async Task<bool> InstallMultipleAsync(string serial, string[] apkPaths, bool reinstall, bool grantPerms, bool allowDowngrade, CancellationToken cancellationToken = default)
        {
            var options = new List<string>();
            if (reinstall) options.Add("-r");
            if (grantPerms) options.Add("-g");
            if (allowDowngrade) options.Add("-d");
            
            var opts = string.Join(" ", options);
            var (success, _) = await InstallMultipleAsync(serial, apkPaths, opts);
            return success;
        }
    }
}
