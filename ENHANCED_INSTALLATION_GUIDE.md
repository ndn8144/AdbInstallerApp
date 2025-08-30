# Enhanced InstallOrchestrator Integration Guide

## Overview

The enhanced InstallOrchestrator provides advanced APK installation capabilities with per-device options, multi-group APK support, and atomic installation safety. This guide shows how to integrate it with existing ViewModels.

## Key Features

### 1. Per-Device Options
- **PreferHighestCompatible**: Auto-select highest compatible versionCode
- **AllowDowngrade**: Enable downgrade installations (-d flag)
- **GrantRuntimePermissions**: Auto-grant permissions (-g flag)
- **Reinstall**: Allow reinstalling existing packages (-r flag)
- **UserId**: Install for specific user (--user flag)
- **StrictSplitMatch**: Control split APK matching behavior
- **InstallStrategy**: Choose between install-multiple vs PM sessions
- **VerifySignature**: Ensure signature consistency in groups
- **VerifyVersionHomogeneity**: Ensure version consistency in groups
- **Throttle**: Limit transfer speed for PM sessions
- **RetryPolicy**: Configurable retry attempts with backoff

### 2. Advanced APK Validation
- Snapshot-based conflict detection
- Package name consistency validation
- Version homogeneity checking
- Signature verification
- Split conflict detection (same profile, different content)
- Device compatibility matching (ABI/DPI/locale)

### 3. Installation Strategies
- **Auto**: Smart selection based on file count and complexity
- **InstallMultiple**: Fast for simple cases, single atomic command
- **PmSession**: Better for complex cases, byte-level progress tracking

### 4. Safety Features
- Package-level locking (prevents concurrent installs of same package)
- Atomic installation at package manager level
- Comprehensive error handling with user-friendly messages
- File integrity validation before installation

## Integration Examples

### Basic Usage with Enhanced Service

```csharp
// In your ViewModel constructor
private readonly EnhancedInstallationService _enhancedInstaller;

public MyViewModel(AdbService adbService)
{
    var adbToolsPath = Path.Combine(AppContext.BaseDirectory, "tools", "platform-tools");
    _enhancedInstaller = new EnhancedInstallationService(adbService, adbToolsPath);
    
    // Set global defaults
    _enhancedInstaller.SetGlobalOptions(
        reinstall: false,
        allowDowngrade: false, 
        grantPermissions: false,
        maxRetries: 2,
        timeout: TimeSpan.FromMinutes(5)
    );
}

// Legacy compatibility installation
public async Task InstallLegacyAsync()
{
    await _enhancedInstaller.InstallLegacyAsync(
        SelectedDevices,
        SelectedApks,
        ReinstallOption,
        GrantPermissionsOption,
        AllowDowngradeOption,
        CancellationToken
    );
}

// Enhanced installation with current options
public async Task InstallEnhancedAsync()
{
    await _enhancedInstaller.InstallAsync(
        SelectedDevices,
        SelectedApkPaths,
        CancellationToken
    );
}
```

### Per-Device Options Configuration

```csharp
// Show device options dialog
public async Task ShowDeviceOptionsAsync(DeviceInfo device)
{
    var viewModel = _enhancedInstaller.CreateDeviceOptionsViewModel(device);
    var dialog = new DeviceOptionsDialog(viewModel);
    
    if (dialog.ShowDialog() == true)
    {
        _enhancedInstaller.ApplyDeviceOptions(viewModel);
        
        // Update UI to show device has custom options
        NotifyDeviceOptionsChanged(device.Serial);
    }
}

// Clear device-specific options
public void ClearDeviceOptions(string deviceSerial)
{
    _enhancedInstaller.ClearDeviceOptions(deviceSerial);
    NotifyDeviceOptionsChanged(deviceSerial);
}

// Check if device has custom options
public bool HasCustomOptions(string deviceSerial)
{
    return _enhancedInstaller.GetDeviceOptions(deviceSerial) != null;
}
```

### Preset Configurations

```csharp
// Apply strict preset (production-ready)
public void ApplyStrictPreset()
{
    var strictOptions = new DeviceInstallOptions(
        Reinstall: false,
        AllowDowngrade: false,
        GrantRuntimePermissions: false,
        PreferHighestCompatible: true,
        StrictSplitMatch: StrictSplitMatchMode.Strict,
        InstallStrategy: InstallStrategy.Auto,
        VerifySignature: true,
        VerifyVersionHomogeneity: true,
        MaxRetries: 2
    );
    
    foreach (var device in SelectedDevices)
    {
        _enhancedInstaller.SetDeviceOptions(device.Serial, strictOptions);
    }
}

// Apply relaxed preset (development-friendly)
public void ApplyRelaxedPreset()
{
    var relaxedOptions = new DeviceInstallOptions(
        Reinstall: true,
        AllowDowngrade: true,
        GrantRuntimePermissions: true,
        StrictSplitMatch: StrictSplitMatchMode.Relaxed,
        VerifySignature: false,
        VerifyVersionHomogeneity: false,
        MaxRetries: 3
    );
    
    foreach (var device in SelectedDevices)
    {
        _enhancedInstaller.SetDeviceOptions(device.Serial, relaxedOptions);
    }
}
```

## UI Integration

### Device List Enhancement

Add device options button to your device list:

```xml
<!-- In your device list item template -->
<StackPanel Orientation="Horizontal">
    <CheckBox IsChecked="{Binding IsSelected}"/>
    <TextBlock Text="{Binding DisplayName}"/>
    
    <!-- Options indicator -->
    <Border Background="Orange" CornerRadius="2" Margin="5,0"
            Visibility="{Binding HasCustomOptions, Converter={StaticResource BoolToVisibilityConverter}}">
        <TextBlock Text="⚙" FontSize="10" Margin="3,1" Foreground="White"/>
    </Border>
    
    <!-- Options button -->
    <Button Content="Options" Margin="5,0" Padding="8,2"
            Command="{Binding DataContext.ShowDeviceOptionsCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
            CommandParameter="{Binding}"/>
</StackPanel>
```

### Progress Integration

The enhanced orchestrator works with the existing `CentralizedProgressService`:

```csharp
// Progress is automatically tracked through the existing system
// No additional integration needed - just use the enhanced service

public async Task InstallWithProgress()
{
    try
    {
        // Progress will be automatically reported to CentralizedProgressService
        await _enhancedInstaller.InstallAsync(devices, paths, cancellationToken);
        
        // Show success message
        ShowMessage("Installation completed successfully!");
    }
    catch (OperationCanceledException)
    {
        ShowMessage("Installation was cancelled.");
    }
    catch (Exception ex)
    {
        ShowMessage($"Installation failed: {ex.Message}");
    }
}
```

## Error Handling

The enhanced system provides detailed error messages:

```csharp
try
{
    await _enhancedInstaller.InstallAsync(devices, paths, cancellationToken);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("INSTALL_FAILED_MISSING_SPLIT"))
{
    ShowMessage("Missing split APKs detected. Try using 'Relaxed' split matching mode or ensure all required splits are selected.");
}
catch (InvalidOperationException ex) when (ex.Message.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE"))
{
    ShowMessage("Signature mismatch detected. The APK has a different signature than the installed version. Uninstall the existing app first or enable 'Reinstall' option.");
}
catch (Exception ex)
{
    ShowMessage($"Installation failed: {ex.Message}");
}
```

## Configuration Validation

Validate settings before installation:

```csharp
public bool ValidateBeforeInstall()
{
    var issues = _enhancedInstaller.ValidateConfiguration();
    if (issues.Any())
    {
        var message = "Configuration issues detected:\n" + string.Join("\n", issues);
        ShowMessage(message);
        return false;
    }
    return true;
}
```

## Migration from Legacy

To migrate existing ViewModels:

1. **Replace InstallOrchestrator** with `EnhancedInstallationService`
2. **Update install calls** to use `InstallLegacyAsync()` for immediate compatibility
3. **Add device options UI** using provided dialog and ViewModel
4. **Gradually migrate** to enhanced features as needed

### Example Migration

```csharp
// Before (legacy)
private readonly InstallOrchestrator _installer;

public async Task InstallAsync()
{
    await _installer.InstallAsync(devices, apks, reinstall, grant, downgrade, log, cancellationToken);
}

// After (enhanced with legacy compatibility)
private readonly EnhancedInstallationService _installer;

public async Task InstallAsync()
{
    await _installer.InstallLegacyAsync(devices, apks, reinstall, grant, downgrade, cancellationToken);
}

// After (fully enhanced)
public async Task InstallAsync()
{
    // Global options set once in constructor
    await _installer.InstallAsync(devices, selectedPaths, cancellationToken);
}
```

## Best Practices

1. **Set global defaults** in ViewModel constructor
2. **Use presets** for common configurations
3. **Validate configuration** before installation
4. **Handle specific errors** with user-friendly messages
5. **Show options indicators** in device list
6. **Provide easy access** to device options dialog
7. **Test with various APK types** (single, splits, different signatures)

## Troubleshooting

### Common Issues

1. **"No valid APK groups found"**: Check file paths and APK integrity
2. **"Missing required splits"**: Use Relaxed mode or add missing splits
3. **"Signature mismatch"**: Enable signature verification bypass or fix signatures
4. **"Device not available"**: Ensure device is online and authorized
5. **"Session creation failed"**: Check device storage and permissions

### Debug Information

Enable debug logging to see detailed validation and matching information:

```csharp
// The enhanced system logs detailed information to System.Diagnostics.Debug
// View in Visual Studio Output window (Debug category)
```

This enhanced system provides a robust, production-ready APK installation solution while maintaining backward compatibility with existing code.
