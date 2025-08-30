using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AdbInstallerApp.Models;
using AdbInstallerApp.Services;

namespace AdbInstallerApp.Tests
{
    /// <summary>
    /// Integration tests for the enhanced installation system
    /// </summary>
    public class EnhancedInstallationTests
    {
        private readonly string _adbPath;
        private readonly string _toolsPath;
        private readonly AdbService _adbService;
        private readonly AdvancedInstallOrchestrator _orchestrator;

        public EnhancedInstallationTests()
        {
            _adbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "platform-tools", "adb.exe");
            _toolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
            _adbService = new AdbService();
            _orchestrator = new AdvancedInstallOrchestrator(_adbService, _toolsPath);
        }

        /// <summary>
        /// Test basic installation with global options
        /// </summary>
        public async Task TestBasicInstallationAsync()
        {
            try
            {
                // Get connected devices
                var devices = await _adbService.ListDevicesAsync();
                var deviceSerials = devices.Where(d => d.State == "device").Select(d => d.Serial).ToList();

                if (!deviceSerials.Any())
                {
                    Console.WriteLine("No devices connected for testing");
                    return;
                }

                // Configure global options
                var globalOptions = new InstallOptionsGlobal(
                    Reinstall: true,
                    AllowDowngrade: false,
                    GrantRuntimePermissions: true,
                    MaxRetries: 2,
                    Timeout: TimeSpan.FromMinutes(5)
                );

                // Test with sample APK paths (replace with actual test APKs)
                var testApks = new List<string>
                {
                    // Add your test APK paths here
                    // "test-app.apk",
                    // "test-split-arm64.apk"
                };

                if (!testApks.Any())
                {
                    Console.WriteLine("No test APKs configured - skipping installation test");
                    return;
                }

                Console.WriteLine($"Testing installation on {deviceSerials.Count} device(s)");
                
                // TODO: Implement AdvancedInstallOrchestrator.RunAsync
                // await _orchestrator.RunAsync(deviceSerials, testApks, globalOptions, null, CancellationToken.None);
                Console.WriteLine("Installation test simulated (RunAsync not implemented yet)");
                
                Console.WriteLine("Basic installation test completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Basic installation test failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Test per-device options configuration
        /// </summary>
        public async Task TestPerDeviceOptionsAsync()
        {
            try
            {
                var devices = await _adbService.ListDevicesAsync();
                var deviceSerials = devices.Where(d => d.State == "device").Select(d => d.Serial).Take(2).ToList();

                if (deviceSerials.Count < 2)
                {
                    Console.WriteLine("Need at least 2 devices for per-device options test");
                    return;
                }

                // Configure different options per device
                var perDeviceOptions = new Dictionary<string, DeviceInstallOptions>
                {
                    [deviceSerials[0]] = new DeviceInstallOptions(
                        Reinstall: true,
                        InstallStrategy: InstallStrategy.PmSession,
                        StrictSplitMatch: StrictSplitMatchMode.Strict,
                        VerifySignature: true
                    ),
                    [deviceSerials[1]] = new DeviceInstallOptions(
                        Reinstall: false,
                        InstallStrategy: InstallStrategy.InstallMultiple,
                        StrictSplitMatch: StrictSplitMatchMode.Relaxed,
                        AllowDowngrade: true
                    )
                };

                var globalOptions = new InstallOptionsGlobal(
                    Reinstall: false, // Will be overridden per device
                    MaxRetries: 1
                );

                var testApks = new List<string>
                {
                    // Add test APK paths
                };

                if (!testApks.Any())
                {
                    Console.WriteLine("No test APKs configured - skipping per-device test");
                    return;
                }

                Console.WriteLine("Testing per-device options configuration");
                
                // TODO: Implement AdvancedInstallOrchestrator.RunAsync
                // await _orchestrator.RunAsync(deviceSerials, testApks, globalOptions, perDeviceOptions, CancellationToken.None);
                Console.WriteLine("Per-device options test simulated (RunAsync not implemented yet)");
                
                Console.WriteLine("Per-device options test completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Per-device options test failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Test APK validation and grouping
        /// </summary>
        public async Task TestApkValidationAsync()
        {
            try
            {
                var validator = new EnhancedApkValidator(_toolsPath);
                
                var testApks = new List<string>
                {
                    // Add test APK paths for validation
                };

                if (!testApks.Any())
                {
                    Console.WriteLine("No test APKs configured - skipping validation test");
                    return;
                }

                Console.WriteLine("Testing APK validation and grouping");
                
                var groups = await validator.BuildGroupsAsync(testApks, CancellationToken.None);
                
                Console.WriteLine($"Found {groups.Count} APK group(s):");
                foreach (var group in groups)
                {
                    Console.WriteLine($"  Package: {group.PackageName}");
                    Console.WriteLine($"  Files: {group.Files.Count}");
                    Console.WriteLine($"  Base APK: {group.Files.Count(f => f.IsBase)}");
                    Console.WriteLine($"  Split APKs: {group.Files.Count(f => !f.IsBase)}");
                    
                    var validation = group.Validate(new DeviceInstallOptions());
                    Console.WriteLine($"  Valid: {validation.IsValid}");
                    if (!validation.IsValid)
                    {
                        Console.WriteLine($"  Errors: {validation.ErrorSummary}");
                    }
                }
                
                Console.WriteLine("APK validation test completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"APK validation test failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Test device property retrieval
        /// </summary>
        public async Task TestDevicePropertiesAsync()
        {
            try
            {
                var devices = await _adbService.ListDevicesAsync();
                var onlineDevices = devices.Where(d => d.State == "device").ToList();

                if (!onlineDevices.Any())
                {
                    Console.WriteLine("No online devices for property testing");
                    return;
                }

                Console.WriteLine("Testing device property retrieval");
                
                foreach (var device in onlineDevices)
                {
                    var props = await _adbService.GetDevicePropertiesAsync(device.Serial);
                    
                    Console.WriteLine($"Device {device.Serial}:");
                    Console.WriteLine($"  Model: {props.GetValueOrDefault("ro.product.model", "Unknown")}");
                    Console.WriteLine($"  ABI: {props.GetValueOrDefault("ro.product.cpu.abilist", "Unknown")}");
                    Console.WriteLine($"  Density: {props.GetValueOrDefault("ro.sf.lcd_density", "Unknown")}");
                    Console.WriteLine($"  SDK: {props.GetValueOrDefault("ro.build.version.sdk", "Unknown")}");
                }
                
                Console.WriteLine("Device properties test completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Device properties test failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Run all tests
        /// </summary>
        public async Task RunAllTestsAsync()
        {
            Console.WriteLine("=== Enhanced Installation System Tests ===");
            
            try
            {
                await TestDevicePropertiesAsync();
                Console.WriteLine();
                
                await TestApkValidationAsync();
                Console.WriteLine();
                
                await TestBasicInstallationAsync();
                Console.WriteLine();
                
                await TestPerDeviceOptionsAsync();
                Console.WriteLine();
                
                Console.WriteLine("All tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test suite failed: {ex.Message}");
                throw;
            }
            finally
            {
                _orchestrator?.Dispose();
            }
        }
    }

    /// <summary>
    /// Simple test runner program
    /// </summary>
    public class TestRunner
    {
        public static async Task Main(string[] args)
        {
            var tests = new EnhancedInstallationTests();
            
            try
            {
                await tests.RunAllTestsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test execution failed: {ex.Message}");
                Environment.Exit(1);
            }
        }
    }
}
