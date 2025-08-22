# APK Manifest Parser & Grouping Features

## Overview
The ADB Installer App now includes advanced APK analysis capabilities with manifest parsing, automatic grouping, and device compatibility analysis.

## 🆕 New Features Implemented

### 1. APK Manifest Parser (ApkAnalyzer)
- **Automatic Manifest Extraction**: Uses `aapt dump badging` to parse APK manifests
- **Comprehensive Information**: Extracts package name, version, permissions, features, and more
- **Split APK Detection**: Automatically identifies base APKs and split APKs
- **Smart Type Classification**: Categorizes APKs by type (Base, ABI, DPI, Language, Other)

#### Key Capabilities:
```csharp
public async Task<ApkManifest> ParseManifestAsync(string apkPath)
public ApkType DetectApkType(string fileName, ApkManifest manifest)
```

#### Extracted Information:
- Package name and version details
- Application label and description
- SDK version requirements (min/target)
- Native code support (ABI information)
- Permissions and features
- Split APK metadata

### 2. Enhanced APK Models

#### ApkManifest Class:
```csharp
public class ApkManifest
{
    public string PackageName { get; set; }
    public string VersionName { get; set; }
    public string VersionCode { get; set; }
    public string AppLabel { get; set; }
    public string TargetSdk { get; set; }
    public string MinSdk { get; set; }
    public List<string> SupportedAbis { get; set; }
    public List<string> Permissions { get; set; }
    public List<string> Features { get; set; }
    public bool IsSplit { get; set; }
    // ... and more
}
```

#### ApkType Enum:
```csharp
public enum ApkType
{
    Base,           // Main application APK
    SplitAbi,       // Architecture-specific splits (arm64, x86)
    SplitDpi,       // Density-specific splits (hdpi, xxhdpi)
    SplitLanguage,  // Language-specific splits (en, vi, fr)
    SplitOther      // Other split types
}
```

### 3. APK Grouping System (ApkRepoIndexer)

#### Automatic Grouping:
- **Package-based Grouping**: Groups APKs by package name
- **Base + Split Organization**: Identifies base APK and related splits
- **Smart Categorization**: Automatically categorizes split types

#### ApkGroup Model:
```csharp
public class ApkGroup
{
    public string PackageName { get; set; }
    public string DisplayName { get; set; }
    public ApkItem BaseApk { get; set; }
    public List<ApkItem> SplitApks { get; set; }
    
    // Convenience properties
    public bool HasValidBase => BaseApk != null;
    public long TotalSize => Base + Splits size
    public bool IsSplit => SplitApks.Count > 0
    
    // Grouped splits by type
    public List<ApkItem> AbiSplits { get; }
    public List<ApkItem> DpiSplits { get; }
    public List<ApkItem> LanguageSplits { get; }
}
```

### 4. Device Compatibility Analysis

#### DeviceCompatibilityAnalyzer:
- **SDK Compatibility Check**: Verifies device API level vs APK requirements
- **ABI Matching**: Finds optimal architecture splits for device
- **DPI Optimization**: Selects best density-specific splits
- **Compatibility Scoring**: Provides percentage-based compatibility rating

#### CompatibilityResult:
```csharp
public class CompatibilityResult
{
    public bool IsCompatible { get; set; }
    public double CompatibilityScore { get; set; }
    public string Reason { get; set; }
    public List<ApkItem> RecommendedSplits { get; set; }
    public ApkItem? OptimalDpi { get; set; }
}
```

### 5. Enhanced Installation Flow

#### InstallOrchestrator Updates:
- **Group-based Installation**: Install entire APK groups instead of individual files
- **Automatic Split Selection**: Automatically chooses optimal splits for each device
- **Install-multiple Support**: Uses `adb install-multiple` for split APKs
- **Compatibility-driven**: Only installs compatible splits for each device

#### New Methods:
```csharp
public async Task InstallGroupsAsync(
    IEnumerable<DeviceInfo> devices,
    IEnumerable<ApkGroup> apkGroups,
    bool reinstall, bool grant, bool downgrade,
    Action<string>? log = null)
```

## 🔧 Technical Implementation

### File Structure:
```
src/AdbInstallerApp/
├── Models/
│   ├── ApkManifest.cs          # APK manifest data model
│   ├── ApkGroup.cs             # APK grouping model
│   └── ApkItem.cs              # Enhanced APK item model
├── Services/
│   ├── ApkAnalyzer.cs          # Manifest parsing service
│   ├── DeviceCompatibilityAnalyzer.cs  # Device compatibility analysis
│   ├── ApkRepoIndexer.cs       # Enhanced repository management
│   └── InstallOrchestrator.cs  # Enhanced installation orchestration
└── ViewModels/
    └── ApkGroupViewModel.cs    # APK group UI model
```

### Key Dependencies:
- **aapt Tool**: Android Asset Packaging Tool for manifest parsing
- **Task.Run**: For non-blocking manifest parsing in UI thread
- **LINQ**: For efficient APK grouping and filtering
- **Regex**: For parsing aapt output

## 🚀 Benefits

### For Users:
1. **Better APK Organization**: Automatic grouping by application
2. **Smart Installation**: Only installs relevant splits for each device
3. **Compatibility Assurance**: Clear indication of device compatibility
4. **Reduced Manual Work**: No need to manually select splits

### For Developers:
1. **Comprehensive APK Analysis**: Full manifest information extraction
2. **Flexible Grouping System**: Easy to extend and customize
3. **Device Compatibility Engine**: Reusable compatibility analysis
4. **Modern Installation Flow**: Support for split APK best practices

## 📱 Usage Examples

### Basic APK Analysis:
```csharp
var analyzer = new ApkAnalyzer();
var manifest = await analyzer.ParseManifestAsync("app.apk");
var type = analyzer.DetectApkType("app-arm64.apk", manifest);
```

### APK Grouping:
```csharp
var indexer = new ApkRepoIndexer();
indexer.SetRepo("C:/APKs");
// Automatically creates groups in indexer.ApkGroups
```

### Compatibility Check:
```csharp
var analyzer = new DeviceCompatibilityAnalyzer();
var result = analyzer.CheckCompatibility(device, apkGroup);
if (result.IsCompatible)
{
    var apksToInstall = BuildInstallList(group, result, options);
    // Install optimal APK combination
}
```

## 🔮 Future Enhancements

### Planned Features:
1. **Advanced Split Detection**: Better recognition of split APK patterns
2. **Performance Optimization**: Caching of parsed manifests
3. **Batch Operations**: Install multiple APK groups simultaneously
4. **Compatibility Reports**: Detailed device compatibility analysis
5. **APK Validation**: Verify APK integrity and compatibility

### Integration Opportunities:
1. **Google Play Store**: Integration with Play Store APK information
2. **Device Profiles**: Save and reuse device compatibility preferences
3. **Installation History**: Track successful installations and failures
4. **APK Repository Management**: Cloud-based APK storage and sharing

## 📋 Requirements

### System Requirements:
- **Android SDK Build Tools**: For aapt tool access
- **.NET 8.0**: Runtime and development environment
- **WPF**: Windows Presentation Foundation for UI

### APK Requirements:
- **Valid APK Files**: Properly formatted Android packages
- **Manifest Access**: APKs must have readable manifests
- **Split APK Support**: For advanced grouping features

## 🎯 Conclusion

The new APK Manifest Parser & Grouping system transforms the ADB Installer App from a simple file installer into a sophisticated APK management and deployment tool. Users can now:

- **Understand APK Contents**: See detailed information about each APK
- **Organize APKs Intelligently**: Automatic grouping by application
- **Install Optimally**: Device-specific split APK selection
- **Ensure Compatibility**: Pre-installation compatibility verification

This foundation enables future enhancements like cloud APK management, advanced device profiling, and enterprise deployment workflows.
