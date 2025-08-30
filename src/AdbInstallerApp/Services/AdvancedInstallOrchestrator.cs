using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdbInstallerApp.Events;
using AdbInstallerApp.Models;
using System.Threading;

namespace AdbInstallerApp.Services
{
    /// <summary>
    /// Advanced InstallOrchestrator with preflight validation, sequential installation, and comprehensive event system
    /// </summary>
    public sealed class AdvancedInstallOrchestrator : IDisposable
    {
        private readonly AdbService _adbService;
        private readonly ApkAnalyzer _apkAnalyzer;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        // Events
        public event EventHandler<InstallationEventArgs>? InstallationEvent;
        public event EventHandler<InstallationProgressArgs>? ProgressUpdated;
        
        // Statistics
        private int _totalDevices;
        private int _totalUnits;
        private int _successfulUnits;
        private int _failedUnits;
        private int _skippedUnits;
        private DateTime _startTime;
        private readonly List<string> _errors = new();
        
        public AdvancedInstallOrchestrator(AdbService adbService, string adbToolsPath)
        {
            _adbService = adbService ?? throw new ArgumentNullException(nameof(adbService));
            _apkAnalyzer = new ApkAnalyzer(adbToolsPath);
            _cancellationTokenSource = new CancellationTokenSource();
        }
        
        /// <summary>
        /// Build installation plans for all selected devices with preflight validation
        /// </summary>
        public async Task<List<DeviceInstallPlan>> BuildInstallationPlansAsync(
            IEnumerable<string> deviceSerials,
            IEnumerable<SelectedItem> selectedItems,
            Models.InstallOptions options,
            CancellationToken cancellationToken = default)
        {
            var plans = new List<DeviceInstallPlan>();
            
            // Step 1: Analyze selected APK files
            RaiseEvent(Events.InstallationEvent.PreflightCheck("", "Analyzing selected APK files..."));
            var apkFiles = await AnalyzeSelectedItemsAsync(selectedItems, cancellationToken);
            
            if (apkFiles.Count == 0)
            {
                RaiseEvent(Events.InstallationEvent.Warning("", "No valid APK files found in selection"));
                return plans;
            }
            
            // Step 2: Group APKs by package
            var installationUnits = _apkAnalyzer.GroupApksByPackage(apkFiles);
            RaiseEvent(Events.InstallationEvent.PreflightCheck("", $"Created {installationUnits.Count} installation unit(s) from {apkFiles.Count} APK file(s)"));
            
            // Step 3: Build plans for each device
            foreach (var serial in deviceSerials)
            {
                try
                {
                    var plan = await BuildDevicePlanAsync(serial, installationUnits, options, cancellationToken);
                    if (plan != null)
                    {
                        plans.Add(plan);
                        RaiseEvent(Events.InstallationEvent.PlanBuilt(serial, plan.Units.Count));
                    }
                }
                catch (Exception ex)
                {
                    RaiseEvent(Events.InstallationEvent.Warning(serial, $"Failed to build plan: {ex.Message}"));
                }
            }
            
            return plans;
        }

        /// <summary>
        /// Main orchestration method with comprehensive error handling and progress tracking
        /// </summary>
        public async Task<InstallationResult> RunAsync(
            IReadOnlyList<string> deviceSerials,
            IReadOnlyList<string> apkPaths,
            AdvancedInstallOptions options,
            IProgress<InstallationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            _startTime = DateTime.Now;
            _totalDevices = deviceSerials.Count;
            _totalUnits = 0; // Will be calculated per device
            _successfulUnits = 0;
            _failedUnits = 0;
            _skippedUnits = 0;
            _errors.Clear();

            RaiseEvent(Events.InstallationEvent.PreflightCheck("", $"Starting installation on {_totalDevices} device(s)"));

            try
            {
                // Phase 1: Validate and analyze APK files
                var apkUnits = await ValidateAndGroupApksAsync(apkPaths, cancellationToken);
                if (apkUnits.Count == 0)
                {
                    return InstallationResult.Failed("No valid APK files found");
                }

                _totalUnits = _totalDevices * apkUnits.Count;

                // Phase 2: Create device-specific installation plans
                var devicePlans = new List<DeviceInstallPlan>();
                for (int i = 0; i < deviceSerials.Count; i++)
                {
                    var serial = deviceSerials[i];
                    var plan = await BuildDevicePlanAsync(serial, apkUnits, new InstallOptions(
                        reinstall: options.Reinstall,
                        grantPermissions: options.GrantPermissions,
                        allowDowngrade: options.AllowDowngrade,
                        maxRetries: options.MaxRetries
                    ), cancellationToken);
                    if (plan != null)
                    {
                        devicePlans.Add(plan);
                    }
                    
                    // Update progress
                    var planningProgress = new InstallationProgress(
                        Phase: InstallationPhase.Planning,
                        CurrentDevice: serial,
                        DeviceIndex: i + 1,
                        TotalDevices: _totalDevices,
                        Message: $"Planning installation for {plan?.Serial ?? "device"}"
                    );
                    progress?.Report(planningProgress);
                }

                if (devicePlans.Count == 0)
                {
                    return InstallationResult.Failed("No devices available for installation");
                }

                // Phase 3: Execute installations sequentially per device
                for (int deviceIndex = 0; deviceIndex < devicePlans.Count; deviceIndex++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var plan = devicePlans[deviceIndex];
                    await ExecuteDevicePlanAsync(plan, deviceIndex, devicePlans.Count, progress, cancellationToken);
                }

                // Phase 4: Generate final results
                var duration = DateTime.Now - _startTime;
                var result = new InstallationResult(
                    IsSuccess: _failedUnits == 0,
                    TotalUnits: _totalUnits,
                    SuccessfulUnits: _successfulUnits,
                    FailedUnits: _failedUnits,
                    SkippedUnits: _skippedUnits,
                    Duration: duration,
                    Errors: _errors.ToList()
                );

                RaiseEvent(Events.InstallationEvent.AllCompleted(new InstallationSummary(
                    TotalDevices: _totalDevices,
                    TotalUnits: _totalUnits,
                    SuccessfulUnits: _successfulUnits,
                    FailedUnits: _failedUnits,
                    SkippedUnits: _skippedUnits,
                    Duration: duration,
                    Errors: _errors
                )));
                return result;
            }
            catch (OperationCanceledException)
            {
                RaiseEvent(Events.InstallationEvent.Warning("", "Installation cancelled by user"));
                throw;
            }
            catch (Exception ex)
            {
                RaiseEvent(Events.InstallationEvent.Warning("", $"Installation orchestration failed: {ex.Message}"));
                return InstallationResult.Failed($"Orchestration failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Execute installation plans sequentially
        /// </summary>
        public async Task<InstallationSummary> ExecuteInstallationPlansAsync(
            IEnumerable<DeviceInstallPlan> plans,
            CancellationToken cancellationToken = default)
        {
            _startTime = DateTime.Now;
            var planList = plans.ToList();
            _totalDevices = planList.Count;
            _totalUnits = planList.Sum(p => p.Units.Count);
            
            RaiseEvent(Events.InstallationEvent.PreflightCheck("", $"Starting sequential installation on {_totalDevices} device(s), {_totalUnits} unit(s)"));
            
            var deviceIndex = 0;
            foreach (var plan in planList)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                    
                deviceIndex++;
                await ExecuteDevicePlanAsync(plan, deviceIndex, _totalDevices, cancellationToken);
            }
            
            var duration = DateTime.Now - _startTime;
            var summary = new InstallationSummary(
                _totalDevices, _totalUnits, _successfulUnits, _failedUnits, _skippedUnits, duration, _errors);
                
            RaiseEvent(Events.InstallationEvent.AllCompleted(summary));
            return summary;
        }
        
        /// <summary>
        /// Cancel all ongoing operations
        /// </summary>
        public void CancelAll()
        {
            _cancellationTokenSource.Cancel();
        }
        
        private async Task<List<ApkFile>> AnalyzeSelectedItemsAsync(
            IEnumerable<SelectedItem> selectedItems,
            CancellationToken cancellationToken)
        {
            var apkFiles = new List<ApkFile>();
            
            foreach (var item in selectedItems)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                    
                switch (item)
                {
                    case SelectedApkFile apkFile:
                        var analyzed = await _apkAnalyzer.AnalyzeApkAsync(apkFile.Path);
                        if (analyzed != null)
                            apkFiles.Add(analyzed);
                        break;
                        
                    case SelectedApkGroup apkGroup:
                        foreach (var filePath in apkGroup.FilePaths)
                        {
                            var groupApk = await _apkAnalyzer.AnalyzeApkAsync(filePath);
                            if (groupApk != null)
                                apkFiles.Add(groupApk);
                        }
                        break;
                }
            }
            
            return apkFiles;
        }
        
        private async Task<DeviceInstallPlan?> BuildDevicePlanAsync(
            string serial,
            List<InstallationUnit> units,
            Models.InstallOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                // Get device properties
                var deviceProps = await GetDevicePropertiesAsync(serial, cancellationToken);
                
                // Handle offline/unauthorized devices
                if (!deviceProps.IsOnline)
                {
                    if (deviceProps.IsUnauthorized)
                        RaiseEvent(Events.InstallationEvent.DeviceUnauthorized(serial));
                    else if (deviceProps.IsOffline)
                        RaiseEvent(Events.InstallationEvent.DeviceOffline(serial));
                    else
                        RaiseEvent(Events.InstallationEvent.Warning(serial, $"Device is {deviceProps.State}. Installation may fail."));
                        
                    // Create plan but mark as non-executable
                    return new DeviceInstallPlan(serial, units, options, deviceProps);
                }
                
                // Match splits to device capabilities
                var matchedUnits = new List<InstallationUnit>();
                foreach (var unit in units)
                {
                    var matchedUnit = _apkAnalyzer.MatchSplitsToDevice(unit, deviceProps);
                    matchedUnits.Add(matchedUnit);
                    
                    if (unit.IsGroup)
                    {
                        var originalSplits = unit.SplitApks.Count();
                        var matchedSplits = matchedUnit.SplitApks.Count();
                        RaiseEvent(Events.InstallationEvent.SplitMatching(serial, unit.PackageName, 
                        matchedUnit.Files.Count, unit.Files.Count));
                    }
                }
                
                return new DeviceInstallPlan(serial, matchedUnits, options, deviceProps);
            }
            catch (Exception ex)
            {
                RaiseEvent(Events.InstallationEvent.Warning(serial, $"Failed to build device plan: {ex.Message}"));
                return null;
            }
        }
        
        private async Task<DeviceProps> GetDevicePropertiesAsync(string serial, CancellationToken cancellationToken)
        {
            try
            {
                // Get device state first
                var devices = await _adbService.ListDevicesAsync();
                var device = devices.FirstOrDefault(d => d.Serial == serial);
                
                if (device == null)
                {
                    return new DeviceProps(serial, "Unknown", "", 0, 0, "offline");
                }
                
                if (device.State != "device")
                {
                    return new DeviceProps(serial, "Unknown", "", 0, 0, device.State);
                }
                
                // Get device properties
                var props = await _adbService.GetDevicePropertiesAsync(serial);
                
                var model = props.GetValueOrDefault("ro.product.model", "Unknown");
                var abilist = props.GetValueOrDefault("ro.product.cpu.abilist", "armeabi-v7a");
                var densityStr = props.GetValueOrDefault("ro.sf.lcd_density", "160");
                var sdkStr = props.GetValueOrDefault("ro.build.version.sdk", "21");
                
                int.TryParse(densityStr, out var density);
                int.TryParse(sdkStr, out var sdk);
                
                return new DeviceProps(serial, model, abilist, density, sdk, device.State);
            }
            catch (Exception ex)
            {
                RaiseEvent(Events.InstallationEvent.Warning(serial, $"Failed to get device properties: {ex.Message}"));
                return new DeviceProps(serial, "Unknown", "armeabi-v7a", 160, 21, "unknown");
            }
        }
        
        private async Task ExecuteDevicePlanAsync(
            DeviceInstallPlan plan,
            int deviceIndex,
            int totalDevices,
            CancellationToken cancellationToken)
        {
            RaiseEvent(Events.InstallationEvent.DeviceStart(plan.Serial, deviceIndex, totalDevices));
            
            if (!plan.CanExecute)
            {
                _skippedUnits += plan.Units.Count;
                RaiseEvent(Events.InstallationEvent.Warning(plan.Serial, "Device not available for installation - skipping"));
                return;
            }
            
            var deviceSuccessful = 0;
            var deviceFailed = 0;
            var deviceSkipped = 0;
            
            var unitIndex = 0;
            foreach (var unit in plan.Units)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                    
                unitIndex++;
                UpdateProgress(plan.Serial, deviceIndex, totalDevices, unitIndex, plan.Units.Count, 
                    $"Installing {unit.PackageName}");
                    
                var success = await ExecuteInstallationUnitAsync(plan.Serial, unit, plan.Options, cancellationToken);
                
                if (success)
                {
                    deviceSuccessful++;
                    _successfulUnits++;
                }
                else
                {
                    deviceFailed++;
                    _failedUnits++;
                }
            }
            
            RaiseEvent(Events.InstallationEvent.DeviceCompleted(plan.Serial, deviceSuccessful, deviceFailed, deviceSkipped));
        }
        
        private async Task<List<InstallationUnit>> ValidateAndGroupApksAsync(
            IReadOnlyList<string> apkPaths, CancellationToken cancellationToken)
        {
            var units = new List<InstallationUnit>();
            
            // Group APKs by package name
            var packageGroups = new Dictionary<string, List<ApkFile>>();
            
            foreach (var path in apkPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    // Use existing ApkAnalyzer methods
                    var apkInfo = await _apkAnalyzer.AnalyzeApkAsync(path);
                    if (apkInfo == null) continue;
                    
                    if (!packageGroups.TryGetValue(apkInfo.PackageName, out var group))
                    {
                        group = new List<ApkFile>();
                        packageGroups[apkInfo.PackageName] = group;
                    }
                    group.Add(apkInfo);
                }
                catch (Exception ex)
                {
                    RaiseEvent(Events.InstallationEvent.Warning("", $"Failed to analyze {Path.GetFileName(path)}: {ex.Message}"));
                }
            }
            
            // Create installation units from groups
            foreach (var (packageName, apkFiles) in packageGroups)
            {
                var baseApks = apkFiles.Where(a => a.IsBase).ToList();
                if (baseApks.Count != 1)
                {
                    RaiseEvent(Events.InstallationEvent.Warning("", $"Package {packageName} has {baseApks.Count} base APKs (expected 1)"));
                    continue;
                }
                
                var splits = apkFiles.Where(a => !a.IsBase).ToList();
                var unit = new InstallationUnit(
                    PackageName: packageName,
                    Files: apkFiles,
                    TotalBytes: apkFiles.Sum(a => a.SizeBytes)
                );
                
                units.Add(unit);
                // Use existing event
                RaiseEvent(Events.InstallationEvent.PreflightCheck("", $"Analyzed package {packageName} with {apkFiles.Count} files"));
            }
            
            return units;
        }

        private async Task ExecuteDevicePlanAsync(
            DeviceInstallPlan plan, 
            int deviceIndex, 
            int totalDevices,
            IProgress<InstallationProgress>? progress,
            CancellationToken cancellationToken)
        {
            RaiseEvent(Events.InstallationEvent.DeviceStart(plan.Serial, deviceIndex + 1, totalDevices));
            
            if (!plan.CanExecute)
            {
                _skippedUnits += plan.Units.Count;
                RaiseEvent(Events.InstallationEvent.Warning(plan.Serial, "Device not available - skipping"));
                return;
            }
            
            for (int unitIndex = 0; unitIndex < plan.Units.Count; unitIndex++)
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                var unit = plan.Units[unitIndex];
                var installProgress = new InstallationProgress(
                    Phase: InstallationPhase.Installing,
                    CurrentDevice: plan.Serial,
                    DeviceIndex: deviceIndex + 1,
                    TotalDevices: totalDevices,
                    CurrentPackage: unit.PackageName,
                    UnitIndex: unitIndex + 1,
                    TotalUnits: plan.Units.Count,
                    Message: $"Installing {unit.PackageName} ({unit.Files.Count} files)"
                );
                progress?.Report(installProgress);
                
                var success = await ExecuteInstallationUnitAsync(plan.Serial, unit, plan.Options, cancellationToken);
                
                if (success)
                {
                    _successfulUnits++;
                    RaiseEvent(Events.InstallationEvent.UnitSuccess(plan.Serial, unit.PackageName, TimeSpan.Zero));
                }
                else
                {
                    _failedUnits++;
                    RaiseEvent(Events.InstallationEvent.UnitFailure(plan.Serial, unit.PackageName, "Installation failed", false));
                }
            }
        }

        private async Task<bool> ExecuteInstallationUnitAsync(
            string serial,
            InstallationUnit unit,
            Models.InstallOptions options,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now;
            
            RaiseEvent(Events.InstallationEvent.UnitStart(serial, unit.PackageName, 0, 0, unit.Files.Count));
            
            for (int attempt = 0; attempt <= options.MaxRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        RaiseEvent(Events.InstallationEvent.RetryAttempt(serial, unit.PackageName, attempt, options.MaxRetries));
                        await Task.Delay(TimeSpan.FromSeconds(1 + attempt), cancellationToken);
                    }
                    
                    bool success;
                    if (unit.IsGroup)
                    {
                        var command = $"install-multiple {options.ToAdbOptions()} {unit.Files.Count} files";
                        RaiseEvent(Events.InstallationEvent.AdbCommand(serial, command));
                        
                        var filePaths = unit.Files.Select(f => f.Path).ToArray();
                        success = await _adbService.InstallMultipleAsync(serial, filePaths, 
                            options.Reinstall, options.GrantPermissions, options.AllowDowngrade, cancellationToken);
                    }
                    else
                    {
                        var apkPath = unit.Files[0].Path;
                        var command = $"install {options.ToAdbOptions()} {Path.GetFileName(apkPath)}";
                        RaiseEvent(Events.InstallationEvent.AdbCommand(serial, command));
                        
                        success = await _adbService.InstallAsync(serial, apkPath, 
                            options.Reinstall, options.GrantPermissions, options.AllowDowngrade, cancellationToken);
                    }
                    
                    if (success)
                    {
                        var duration = DateTime.Now - startTime;
                        RaiseEvent(Events.InstallationEvent.UnitSuccess(serial, unit.PackageName, duration));
                        return true;
                    }
                    else
                    {
                        var canRetry = attempt < options.MaxRetries;
                        RaiseEvent(Events.InstallationEvent.UnitFailure(serial, unit.PackageName, "Installation failed", canRetry));
                        
                        if (!canRetry)
                        {
                            _errors.Add($"{serial}: {unit.PackageName} - Installation failed after {options.MaxRetries + 1} attempts");
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    var canRetry = attempt < options.MaxRetries;
                    RaiseEvent(Events.InstallationEvent.UnitFailure(serial, unit.PackageName, ex.Message, false));
                    
                    if (!canRetry)
                    {
                        _errors.Add($"{serial}: {unit.PackageName} - {ex.Message}");
                        return false;
                    }
                }
            }
            
            return false;
        }

        /// <summary>
        /// Install using PM session for complex APK groups
        /// </summary>
        private async Task<bool> InstallUsingPmSessionAsync(
            string serial,
            InstallationUnit unit,
            Models.InstallOptions options,
            CancellationToken cancellationToken)
        {
            string? sessionId = null;
            try
            {
                // Create session
                var sessionOptions = new InstallSessionOptions(
                    Reinstall: options.Reinstall,
                    AllowDowngrade: options.AllowDowngrade,
                    GrantPermissions: options.GrantPermissions,
                    UserId: null
                );
                
                sessionId = await _adbService.CreateInstallSessionAsync(serial, sessionOptions, cancellationToken);
                RaiseEvent(Events.InstallationEvent.AdbCommand(serial, $"pm install-create session {sessionId}"));

                // Write all APK files to session
                var totalBytes = unit.TotalBytes;
                var writtenBytes = 0L;
                
                foreach (var apkFile in unit.Files)
                {
                    var fileProgress = new Progress<long>(bytes =>
                    {
                        var newWritten = Interlocked.Add(ref writtenBytes, bytes);
                        var percentage = (double)newWritten / totalBytes * 100;
                        // Use UnitStart to show progress since Progress method doesn't exist
                        RaiseEvent(Events.InstallationEvent.UnitStart(serial, unit.PackageName, 0, 0, unit.Files.Count));
                    });
                    
                    await _adbService.WriteToSessionAsync(serial, sessionId, apkFile.Path, fileProgress, cancellationToken);
                }

                // Commit session
                RaiseEvent(Events.InstallationEvent.AdbCommand(serial, $"pm install-commit {sessionId}"));
                await _adbService.CommitInstallSessionAsync(serial, sessionId, cancellationToken);
                
                return true;
            }
            catch (InstallationException iex) when (iex.ErrorType == InstallErrorType.MissingSplit)
            {
                RaiseEvent(Events.InstallationEvent.Warning(serial, $"Missing required splits for {unit.PackageName} - consider using relaxed split matching"));
                return false;
            }
            catch (Exception)
            {
                // Clean up session on failure
                if (sessionId != null)
                {
                    try
                    {
                        await _adbService.AbandonInstallSessionAsync(serial, sessionId, CancellationToken.None);
                    }
                    catch
                    {
                        // Ignore cleanup failures
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// Determine the best installation strategy for a unit
        /// </summary>
        private InstallStrategy DetermineInstallStrategy(InstallationUnit unit, Models.InstallOptions options)
        {
            // Use PM session for complex groups or when paths are long
            var hasLongPaths = unit.Files.Any(f => f.Path.Length > 200 || f.Path.Contains(' '));
            var hasManyFiles = unit.Files.Count >= 8;
            
            if (unit.IsGroup && (hasLongPaths || hasManyFiles))
            {
                return InstallStrategy.PmSession;
            }
            
            if (unit.IsGroup)
            {
                return InstallStrategy.InstallMultiple;
            }
            
            return InstallStrategy.Auto; // Will default to single APK
        }
        
        private void RaiseEvent(Events.InstallationEvent evt)
        {
            InstallationEvent?.Invoke(this, new InstallationEventArgs(evt));
        }
        
        private void UpdateProgress(string serial, int currentDevice, int totalDevices, 
            int currentUnit, int totalUnits, string operation)
        {
            var overallProgress = totalDevices > 0 ? 
                ((currentDevice - 1) * 100.0 / totalDevices) + 
                (currentUnit * 100.0 / totalDevices / totalUnits) : 0;
                
            ProgressUpdated?.Invoke(this, new InstallationProgressArgs(
                serial, currentDevice, totalDevices, currentUnit, totalUnits, operation, overallProgress));
        }
        
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}
