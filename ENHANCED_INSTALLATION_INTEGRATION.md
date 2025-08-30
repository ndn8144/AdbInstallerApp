# Enhanced Installation System Integration Guide

## Overview

The enhanced installation system provides advanced APK installation capabilities with per-device options, conflict detection, and atomic installation safety. This guide shows how to integrate and use the enhanced system.

## Key Components

### 1. Enhanced Models
- `DeviceInstallOptions` - Per-device installation configuration
- `InstallOptionsGlobal` - Global fallback options
- `DevicePlan` - Device-specific installation plan
- `GroupSnapshot` - APK group validation and conflict detection

### 2. Core Services
- `EnhancedInstallOrchestrator` - Main orchestration service
- `EnhancedApkValidator` - APK validation and grouping
- `InstallStrategies` - Installation strategy implementations
- `EnhancedInstallationService` - Integration bridge service

### 3. UI Components
- `DeviceOptionsDialog` - Per-device options configuration
- `DeviceOptionsViewModel` - Options dialog view model

## Basic Usage

### Simple Installation with Global Options

```csharp
// Initialize services
var adbService = new AdbService("path/to/adb.exe");
var orchestrator = new EnhancedInstallOrchestrator(adbService, "path/to/tools");

// Configure global options
var globalOptions = new InstallOptionsGlobal(
    Reinstall: true,
    AllowDowngrade: false,
    GrantRuntimePermissions: true,
    MaxRetries: 3
);

// Run installation
var devices = new[] { "device_serial_1", "device_serial_2" };
var apkPaths = new[] { "app.apk", "split1.apk", "split2.apk" };

await orchestrator.RunAsync(devices, apkPaths, globalOptions);
```

### Advanced Installation with Per-Device Options

```csharp
// Configure per-device options
var perDeviceOptions = new Dictionary<string, DeviceInstallOptions>
{
    ["device_serial_1"] = new DeviceInstallOptions(
        Reinstall: true,
        InstallStrategy: InstallStrategy.PmSession,
        StrictSplitMatch: StrictSplitMatchMode.Strict,
        ThrottleMBps: 10,
        VerifySignature: true
    ),
    ["device_serial_2"] = new DeviceInstallOptions(
        Reinstall: false,
        InstallStrategy: InstallStrategy.InstallMultiple,
        StrictSplitMatch: StrictSplitMatchMode.Relaxed,
        AllowDowngrade: true
    )
};

// Run with per-device options
await orchestrator.RunAsync(devices, apkPaths, globalOptions, perDeviceOptions);
```

## Integration with Existing UI

### Using EnhancedInstallationService

```csharp
// In your main window or view model
public class MainWindowViewModel
{
    private readonly EnhancedInstallationService _installService;
    
    public MainWindowViewModel()
    {
        var adbService = new AdbService("path/to/adb.exe");
        _installService = new EnhancedInstallationService(adbService, "path/to/tools");
    }
    
    // Legacy compatibility method
    public async Task InstallLegacyAsync(List<string> devices, List<string> paths)
    {
        await _installService.InstallAsync(devices, paths);
    }
    
    // Enhanced installation with options
    public async Task InstallEnhancedAsync(List<string> devices, List<string> paths)
    {
        var globalOptions = new InstallOptionsGlobal(Reinstall: true);
        await _installService.InstallWithOptionsAsync(devices, paths, globalOptions);
    }
    
    // Show device options dialog
    public void ShowDeviceOptions(string deviceSerial)
    {
        _installService.ShowDeviceOptionsDialog(deviceSerial);
    }
}
```

### Device Options Dialog Integration

```csharp
// Show device options dialog
var dialog = new DeviceOptionsDialog();
var viewModel = new DeviceOptionsViewModel(deviceSerial, currentOptions);
dialog.DataContext = viewModel;

if (dialog.ShowDialog() == true)
{
    var updatedOptions = viewModel.GetDeviceOptions();
    // Use updated options for installation
}
```

## Configuration Options

### DeviceInstallOptions Properties

| Property | Description | Default |
|----------|-------------|---------|
| `Reinstall` | Allow package replacement | `false` |
| `AllowDowngrade` | Allow version downgrades | `false` |
| `GrantRuntimePermissions` | Auto-grant permissions | `false` |
| `UserId` | Target user ID | `null` |
| `PreferHighestCompatible` | Prefer highest compatible splits | `true` |
| `StrictSplitMatch` | Split matching behavior | `Strict` |
| `InstallStrategy` | Installation method | `Auto` |
| `VerifySignature` | Verify APK signatures | `true` |
| `VerifyVersionHomogeneity` | Verify version consistency | `true` |
| `ThrottleMBps` | Transfer rate limit | `null` |
| `MaxRetries` | Retry attempts | `2` |
| `Timeout` | Operation timeout | `5 minutes` |

### StrictSplitMatchMode Options

- **Strict**: Require all mandatory splits (ABI/DPI/requiredSplit/feature)
- **Relaxed**: Skip non-matching splits (ABI/DPI/locale)
- **BaseOnlyFallback**: Fall back to base-only installation if splits don't match

### InstallStrategy Options

- **Auto**: Auto-select based on file count and complexity
- **InstallMultiple**: Use `adb install-multiple` command
- **PmSession**: Use `pm install-create/write/commit` session

## Progress Tracking

The enhanced system provides detailed progress tracking:

```csharp
// Subscribe to progress events
CentralizedProgressService.Instance.ProgressChanged += (sender, e) =>
{
    Console.WriteLine($"Progress: {e.ProgressPercentage}% - {e.StatusMessage}");
};

// Progress is automatically reported during:
// - APK validation (15% weight)
// - Session creation (10% weight)
// - File transfer (60% weight)
// - Installation commit (15% weight)
```

## Error Handling

The system provides comprehensive error handling:

```csharp
try
{
    await orchestrator.RunAsync(devices, apkPaths, globalOptions);
}
catch (InvalidOperationException ex)
{
    // Validation or configuration errors
    Console.WriteLine($"Configuration error: {ex.Message}");
}
catch (OperationCanceledException)
{
    // User cancellation
    Console.WriteLine("Installation cancelled by user");
}
catch (Exception ex)
{
    // Installation failures
    Console.WriteLine($"Installation failed: {ex.Message}");
}
```

## Advanced Features

### APK Group Validation

The system automatically:
- Groups APKs by package name
- Validates split APK integrity
- Detects version and signature conflicts
- Filters incompatible splits per device

### Atomic Installation Safety

- Package-level locking prevents concurrent installations
- PM session strategy provides atomic commits
- Rollback on failure (where supported by Android)
- Retry logic with exponential backoff

### Device Compatibility Matching

- ABI compatibility checking
- DPI density matching
- Locale preference handling
- SDK level validation

## Migration from Legacy System

### Step 1: Replace InstallOrchestrator

```csharp
// Old code
var orchestrator = new InstallOrchestrator(adbService, progressService);
await orchestrator.RunAsync(devices, paths, options, null, cancellationToken);

// New code
var enhancedOrchestrator = new EnhancedInstallOrchestrator(adbService, toolsPath);
var globalOptions = new InstallOptionsGlobal(/* map from old options */);
await enhancedOrchestrator.RunAsync(devices, paths, globalOptions, null, cancellationToken);
```

### Step 2: Use Integration Service for Compatibility

```csharp
// Use EnhancedInstallationService for seamless integration
var installService = new EnhancedInstallationService(adbService, toolsPath);

// Legacy method calls still work
await installService.InstallAsync(devices, paths);

// Enhanced features available when needed
await installService.InstallWithOptionsAsync(devices, paths, globalOptions, perDeviceOptions);
```

### Step 3: Update UI for Per-Device Options

```csharp
// Add device options button to device list
private void DeviceOptionsButton_Click(object sender, RoutedEventArgs e)
{
    if (sender is Button button && button.Tag is string deviceSerial)
    {
        _installService.ShowDeviceOptionsDialog(deviceSerial);
    }
}
```

## Best Practices

1. **Use Auto Strategy**: Let the system choose the best installation method
2. **Enable Validation**: Keep signature and version verification enabled
3. **Configure Timeouts**: Set appropriate timeouts for your environment
4. **Handle Cancellation**: Always support user cancellation
5. **Monitor Progress**: Subscribe to progress events for UI updates
6. **Test Thoroughly**: Validate with various APK groups and device configurations

## Troubleshooting

### Common Issues

1. **InitializeComponent Error**: Rebuild the project to generate XAML code-behind
2. **Missing Dependencies**: Ensure all NuGet packages are restored
3. **ADB Path Issues**: Verify ADB tools path is correctly configured
4. **Permission Errors**: Check device authorization and USB debugging

### Debug Logging

Enable debug output to troubleshoot issues:

```csharp
System.Diagnostics.Debug.WriteLine("Enhanced installation debug info");
```

## Performance Considerations

- **Large APK Groups**: Use PM session strategy for better progress tracking
- **Multiple Devices**: Installation runs in parallel per device
- **Network Throttling**: Configure ThrottleMBps for slow connections
- **Memory Usage**: APK validation loads metadata but not full file content

This enhanced installation system provides a robust, flexible foundation for APK installation with advanced validation, per-device configuration, and comprehensive error handling.
