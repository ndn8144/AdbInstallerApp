using AdbInstallerApp.Helpers;
using AdbInstallerApp.Models;
using System.Text.RegularExpressions;
using System.IO;


namespace AdbInstallerApp.Services
{
    public class AdbService
    {
        private readonly string _adbPath;


        public AdbService()
        {
            // Prefer bundled adb under solution root / tools/platform-tools
            var baseDir = AppContext.BaseDirectory;
            var toolsCandidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "tools", "platform-tools", "adb.exe"));
            if (File.Exists(toolsCandidate)) _adbPath = toolsCandidate;
            else _adbPath = "adb"; // fallback to PATH
        }

        public string AdbPath => _adbPath;

        public async Task StartServerAsync()
        {
            await ProcessRunner.RunAsync(_adbPath, "start-server");
        }


        public async Task<List<DeviceInfo>> ListDevicesAsync()
        {
            var list = new List<DeviceInfo>();
            var (code, stdout, _) = await ProcessRunner.RunAsync(_adbPath, "devices");
            if (code != 0) return list;

            var lines = stdout.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines.Skip(1)) // skip header
            {
                // Format: SERIAL\tSTATE
                var parts = Regex.Split(line.Trim(), @"\s+");
                if (parts.Length >= 2)
                {
                    var device = new DeviceInfo { Serial = parts[0], State = parts[1] };
                    if (device.State == "device")
                    {
                        await EnrichDeviceInfoAsync(device);
                    }
                    list.Add(device);
                }
            }
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
            var (code, stdout, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell getprop");
            if (code == 0)
            {
                var lines = stdout.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains(": "))
                    {
                        var parts = line.Split(new[] {": "}, 2, StringSplitOptions.None);
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
            var (cpuCode, cpuOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell cat /proc/cpuinfo");
            if (cpuCode == 0)
            {
                device.CpuInfo = cpuOutput;
            }

            // Memory Information
            var (memCode, memOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell cat /proc/meminfo");
            if (memCode == 0)
            {
                device.MemoryInfo = memOutput;
            }

            // Storage Information
            var (storageCode, storageOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell df");
            if (storageCode == 0)
            {
                device.StorageInfo = storageOutput;
            }

            // Partition Information
            var (partCode, partOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell cat /proc/partitions");
            if (partCode == 0)
            {
                device.PartitionInfo = partOutput;
            }

            // Battery Information
            var (batteryCode, batteryOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell dumpsys battery");
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
            var (capacityCode, capacityOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell cat /sys/class/power_supply/battery/capacity");
            if (capacityCode == 0 && string.IsNullOrEmpty(device.BatteryLevel))
            {
                device.BatteryLevel = capacityOutput.Trim();
            }
        }

        private async Task GetNetworkInfoAsync(DeviceInfo device)
        {
            // Network Interfaces
            var (netCode, netOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell ip addr show");
            if (netCode == 0)
            {
                device.NetworkInterfaces = netOutput;
            }

            // WiFi Information
            var (wifiCode, wifiOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell dumpsys wifi");
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
            var (connCode, connOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell dumpsys connectivity");
            if (connCode == 0)
            {
                device.ConnectivityInfo = connOutput;
                device.HasMobileData = connOutput.Contains("mobile") || connOutput.Contains("cellular");
            }

            // Network Statistics
            var (statsCode, statsOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell cat /proc/net/dev");
            if (statsCode == 0)
            {
                device.NetworkStats = statsOutput;
            }
        }

        private async Task GetDisplayInfoAsync(DeviceInfo device)
        {
            // Screen Resolution
            var (sizeCode, sizeOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell wm size");
            if (sizeCode == 0)
            {
                device.ScreenResolution = sizeOutput.Trim();
            }

            // Screen Density
            var (densityCode, densityOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell wm density");
            if (densityCode == 0)
            {
                device.ScreenDensity = densityOutput.Trim();
            }

            // Display Information
            var (displayCode, displayOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell dumpsys display");
            if (displayCode == 0)
            {
                device.DisplayInfo = displayOutput;
            }

            // Graphics Information
            var (graphicsCode, graphicsOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell dumpsys SurfaceFlinger");
            if (graphicsCode == 0)
            {
                device.GraphicsInfo = graphicsOutput;
            }
        }

        private async Task GetSecurityInfoAsync(DeviceInfo device)
        {
            // SELinux Mode
            var (selinuxCode, selinuxOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell getenforce");
            if (selinuxCode == 0)
            {
                device.SelinuxMode = selinuxOutput.Trim();
            }

            // Kernel Version
            var (kernelCode, kernelOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell cat /proc/version");
            if (kernelCode == 0)
            {
                device.KernelVersion = kernelOutput.Trim();
            }

            // Check if device is rooted
            var (rootCode, rootOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell which su");
            device.IsRooted = rootCode == 0 && !string.IsNullOrWhiteSpace(rootOutput.Trim());
            if (device.IsRooted)
            {
                device.SuBinaryLocation = rootOutput.Trim();
            }

            // Check for Superuser app
            var (superuserCode, superuserOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell ls /system/app/Superuser.apk");
            device.HasSuperuserApp = superuserCode == 0 && !string.IsNullOrWhiteSpace(superuserOutput.Trim());

            // Check for xbin tools
            var (xbinCode, xbinOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell ls /system/xbin/which");
            device.HasXbinTools = xbinCode == 0 && !string.IsNullOrWhiteSpace(xbinOutput.Trim());
        }

        private async Task GetRuntimeInfoAsync(DeviceInfo device)
        {
            // Package Information
            var (packagesCode, packagesOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell pm list packages");
            if (packagesCode == 0)
            {
                var packages = packagesOutput.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
                device.TotalPackages = packages.Length;
            }

            // Third-party packages
            var (thirdPartyCode, thirdPartyOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell pm list packages -3");
            if (thirdPartyCode == 0)
            {
                var thirdPartyPackages = thirdPartyOutput.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
                device.ThirdPartyPackages = thirdPartyPackages.Length;
            }

            // System packages
            var (systemCode, systemOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell pm list packages -s");
            if (systemCode == 0)
            {
                var systemPackages = systemOutput.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
                device.SystemPackages = systemPackages.Length;
            }

            // Disabled packages
            var (disabledCode, disabledOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell pm list packages -d");
            if (disabledCode == 0)
            {
                var disabledPackages = disabledOutput.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
                device.DisabledPackages = disabledPackages.Length;
            }

            // Process Information
            var (processCode, processOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell ps");
            if (processCode == 0)
            {
                device.ProcessInfo = processOutput;
            }

            // CPU Usage
            var (cpuUsageCode, cpuUsageOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell top -n 1");
            if (cpuUsageCode == 0)
            {
                device.CpuUsage = cpuUsageOutput;
            }

            // Activity Manager Information
            var (activityCode, activityOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell dumpsys activity");
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
                var (tempCode, tempOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell dumpsys battery | grep temperature");
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
            var (thermalCode, thermalOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell cat /sys/class/thermal/thermal_zone*/temp");
            if (thermalCode == 0)
            {
                device.ThermalZones = thermalOutput;
            }

            // Sensor Information
            var (sensorCode, sensorOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell dumpsys sensorservice");
            if (sensorCode == 0)
            {
                device.SensorInfo = sensorOutput;
            }
        }

        private async Task GetDeveloperOptionsStatusAsync(DeviceInfo device)
        {
            // Developer Options Enabled
            var (devOptsCode, devOptsOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell settings get global development_settings_enabled");
            if (devOptsCode == 0)
            {
                device.DeveloperOptionsEnabled = devOptsOutput.Trim() == "1";
            }

            // ADB Enabled
            var (adbCode, adbOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell settings get global adb_enabled");
            if (adbCode == 0)
            {
                device.AdbEnabled = adbOutput.Trim() == "1";
            }

            // USB Debugging Enabled
            var (usbDebugCode, usbDebugOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell settings get global usb_debugging_enabled");
            if (usbDebugCode == 0)
            {
                device.UsbDebuggingEnabled = usbDebugOutput.Trim() == "1";
            }

            // Verify Apps Over USB
            var (verifyCode, verifyOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell settings get global verify_apps_over_usb");
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
            var args = $"-s {serial} install {opts} \"{apkPath}\"";
            var (code, so, se) = await ProcessRunner.RunAsync(_adbPath, args);
            return (code == 0, so + se);
        }


        public async Task<(bool ok, string log)> InstallMultipleAsync(string serial, IEnumerable<string> apkPaths, string opts)
        {
            var joined = string.Join(" ", apkPaths.Select(p => $"\"{p}\""));
            var args = $"-s {serial} install-multiple {opts} {joined}";
            var (code, so, se) = await ProcessRunner.RunAsync(_adbPath, args);
            return (code == 0, so + se);
        }
    }
}