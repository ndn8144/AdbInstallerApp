# Module "Installed Apps" - Tài liệu Kỹ thuật

## Tổng quan
Module "Installed Apps" cung cấp chức năng hiển thị danh sách các ứng dụng đã cài đặt trên thiết bị Android được chọn, với khả năng lọc, tìm kiếm và xuất APK files.

## Kiến trúc Module

### 1. Models (`Models/InstallApp.cs`)

#### `InstalledApp` Class
```csharp
public sealed class InstalledApp : INotifyPropertyChanged
```

**Chức năng chính:**
- Lưu trữ thông tin chi tiết về một ứng dụng đã cài đặt
- Hỗ trợ data binding với UI thông qua `INotifyPropertyChanged`
- Quản lý trạng thái selection cho việc xuất APK

**Thuộc tính quan trọng:**
- `PackageName`: Tên package (com.example.app)
- `DisplayName`: Tên hiển thị của app
- `VersionInfo`: Thông tin phiên bản
- `IsSystemApp`: Phân biệt System/User app
- `HasSplits`: Kiểm tra app có split APKs không
- `IsSelected`: Trạng thái được chọn để xuất
- `CodePaths`: Đường dẫn các file APK trên thiết bị

#### `ExportResult` Record
```csharp
public sealed record ExportResult(...)
```
Lưu trữ kết quả của quá trình xuất APK, bao gồm:
- Danh sách file đã xuất
- Trạng thái thành công/thất bại
- Thông báo lỗi (nếu có)

#### `TransferProgress` Record
```csharp
public sealed record TransferProgress(...)
```
Theo dõi tiến trình transfer file từ thiết bị:
- Phần trăm hoàn thành
- Tốc độ transfer
- Thời gian ước tính

### 2. Services

#### `AppInventoryService` (`Services/AppInventoryService.cs`)

**Chức năng:**
- Lấy danh sách ứng dụng từ thiết bị Android qua ADB
- Phân tích thông tin chi tiết từ `dumpsys package`
- Tính toán kích thước APK files

**Quy trình hoạt động:**
```
1. Chạy lệnh: adb shell cmd package list packages -f --user 0
2. Parse kết quả để lấy package name và đường dẫn
3. Với mỗi package:
   - Lấy thông tin chi tiết từ dumpsys package
   - Tính kích thước file (nếu được yêu cầu)
   - Xác định loại app (System/User)
4. Áp dụng filters và sắp xếp
```

**Tối ưu hóa:**
- Sử dụng `SemaphoreSlim` để giới hạn concurrent requests
- Fallback từ `cmd package` về `pm` cho thiết bị cũ
- Parallel processing cho việc enrichment data

#### `ApkExportService` (`Services/ApkExportService.cs`)

**Chức năng:**
- Xuất APK files từ thiết bị về máy tính
- Hỗ trợ xuất cả base APK và split APKs
- Xử lý các trường hợp lỗi và retry logic

**Quy trình xuất APK:**
```
1. Lấy đường dẫn APK files: adb shell pm path <package>
2. Tạo thư mục đích trên máy tính
3. Với mỗi APK file:
   - Chạy: adb pull -p <remote_path> <local_path>
   - Theo dõi progress từ stderr output
   - Fallback methods nếu pull thất bại
4. Verify file integrity
```

**Xử lý lỗi:**
- Alternative pull method qua `/sdcard/` cho protected apps
- Retry logic với exponential backoff
- Detailed error reporting

### 3. ViewModel (`ViewModels/InstalledAppViewModel.cs`)

#### `InstalledAppViewModel` Class
```csharp
public partial class InstalledAppViewModel : ObservableObject
```

**Chức năng chính:**
- Quản lý state của UI (loading, exporting, filtering)
- Xử lý user interactions (commands)
- Data binding với View
- Progress reporting và logging

**Commands được implement:**
- `LoadAppsCommand`: Tải danh sách apps từ thiết bị
- `RefreshAppsCommand`: Refresh danh sách
- `ExportSelectedAppsCommand`: Xuất apps đã chọn
- `ExportAllFilteredAppsCommand`: Xuất tất cả apps đã filter
- `SelectAllCommand/DeselectAllCommand`: Chọn/bỏ chọn tất cả
- `CancelLoadCommand/CancelExportCommand`: Hủy operations

**Filtering Logic:**
```csharp
private void ApplyFilters()
{
    // Filter by app type (All/User/System)
    // Filter by keyword (package name or display name)
    // Sort by display name
    // Update UI collections
}
```

**Progress Tracking:**
- Real-time status updates
- Detailed export logging
- Cancellation support
- Error handling với user-friendly messages

### 4. View (`Views/InstalledAppsView.xaml`)

#### UI Components

**Header Section:**
- Title và status message
- App counts (total, selected)
- Refresh button

**Filter Controls:**
- Search textbox với placeholder
- App type filter (ComboBox)
- Include splits checkbox
- Select/Deselect all buttons

**Main DataGrid:**
- Checkbox column cho selection
- App information columns
- Sortable headers
- Modern styling với hover effects

**Progress Overlays:**
- Loading overlay với progress bar
- Export overlay với cancel button
- Semi-transparent background

**Export Log Section:**
- Scrollable text area
- Console-style font (Consolas)
- Real-time log updates

## Cách Code Hoạt Động

### 1. Initialization Flow
```
1. User chọn device từ main window
2. InstalledAppViewModel.SetSelectedDevice() được gọi
3. LoadAppsCommand được enable
4. User click "Load Apps" hoặc module tự động load
```

### 2. App Loading Process
```
1. UI hiển thị loading overlay
2. AppInventoryService.ListInstalledAppsAsync() được gọi
3. Service thực hiện:
   - List packages qua ADB
   - Parallel enrichment với SemaphoreSlim
   - Apply filters theo user settings
4. UI update với danh sách apps
5. Loading overlay ẩn đi
```

### 3. Filtering & Search
```
1. User nhập keyword hoặc thay đổi filter
2. PropertyChanged event trigger ApplyFilters()
3. LINQ queries áp dụng filters:
   - App type filter
   - Keyword search (package name + display name)
4. FilteredApps collection được update
5. UI tự động refresh qua data binding
```

### 4. APK Export Process
```
1. User chọn apps và click Export
2. Folder browser dialog mở
3. Export overlay hiển thị
4. ApkExportService.ExportMultipleApksAsync():
   - Tạo thư mục cho mỗi package
   - Parallel export với progress reporting
   - Error handling và retry logic
5. Export log được update real-time
6. Completion notification
```

### 5. Error Handling Strategy
```
- Network errors: Retry với exponential backoff
- Permission errors: Alternative methods
- User cancellation: Clean cancellation tokens
- UI errors: User-friendly error messages
- Logging: Detailed logs cho debugging
```

## Tối Ưu Hóa Hiện Tại

### Performance Optimizations
1. **Concurrent Processing**: SemaphoreSlim giới hạn 3 concurrent ADB calls
2. **Lazy Loading**: Size calculation chỉ khi cần thiết
3. **Efficient Filtering**: LINQ với early termination
4. **Memory Management**: Proper disposal của resources

### UX Optimizations
1. **Progress Indicators**: Visual feedback cho mọi operations
2. **Cancellation Support**: User có thể hủy long-running operations
3. **Real-time Updates**: Status và progress updates
4. **Error Recovery**: Graceful error handling với retry options

## Gợi Ý Cải Tiến Tương Lai

### 1. Performance Enhancements

#### Caching Strategy
```csharp
// Implement app list caching
public class AppCacheService
{
    private readonly Dictionary<string, CachedAppList> _cache = new();
    
    public async Task<IReadOnlyList<InstalledApp>> GetCachedAppsAsync(
        string deviceSerial, 
        TimeSpan maxAge)
    {
        // Check cache validity
        // Return cached data if fresh
        // Otherwise refresh and cache
    }
}
```

#### Background Refresh
```csharp
// Auto-refresh app list periodically
private readonly Timer _refreshTimer;

private void StartBackgroundRefresh()
{
    _refreshTimer = new Timer(async _ => 
    {
        if (!IsLoading && !IsExporting)
            await RefreshAppsAsync();
    }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
}
```

### 2. Advanced Filtering

#### Multi-criteria Filtering
```csharp
public class AdvancedFilter
{
    public string? PackageNamePattern { get; set; }
    public string? DisplayNamePattern { get; set; }
    public DateRange? InstallDateRange { get; set; }
    public SizeRange? SizeRange { get; set; }
    public bool? HasSplits { get; set; }
    public int? MinTargetSdk { get; set; }
}
```

#### Saved Filter Presets
```csharp
public class FilterPreset
{
    public string Name { get; set; }
    public AdvancedFilter Filter { get; set; }
    public bool IsDefault { get; set; }
}
```

### 3. Enhanced Export Features

#### Batch Export với Resume
```csharp
public class BatchExportManager
{
    public async Task<ExportResult> ExportWithResumeAsync(
        IEnumerable<string> packages,
        string destDir,
        ExportOptions options,
        CancellationToken ct)
    {
        // Save export state
        // Support resume from interruption
        // Verify existing files
    }
}
```

#### Export Templates
```csharp
public class ExportTemplate
{
    public string Name { get; set; }
    public string PathPattern { get; set; } // {PackageName}/{Version}/
    public bool IncludeSplits { get; set; }
    public bool CreateManifest { get; set; }
    public CompressionLevel Compression { get; set; }
}
```

### 4. Advanced UI Features

#### Virtual DataGrid
```csharp
// Implement virtualization cho large app lists
<DataGrid VirtualizingPanel.IsVirtualizing="True"
          VirtualizingPanel.VirtualizationMode="Recycling"
          VirtualizingPanel.ScrollUnit="Item"/>
```

#### Column Customization
```csharp
public class ColumnSettings
{
    public Dictionary<string, bool> VisibleColumns { get; set; }
    public Dictionary<string, double> ColumnWidths { get; set; }
    public List<string> ColumnOrder { get; set; }
}
```

### 5. Integration Enhancements

#### Plugin Architecture
```csharp
public interface IAppAnalyzer
{
    Task<AppAnalysisResult> AnalyzeAsync(InstalledApp app);
}

// Plugins: Security scanner, Performance analyzer, etc.
```

#### External Tool Integration
```csharp
public class ExternalToolManager
{
    public async Task OpenInApkAnalyzer(string apkPath);
    public async Task DecompileWithJadx(string apkPath);
    public async Task ScanWithVirusTotal(string apkPath);
}
```

### 6. Data Persistence

#### Export History
```csharp
public class ExportHistory
{
    public DateTime ExportDate { get; set; }
    public string DeviceSerial { get; set; }
    public List<string> ExportedPackages { get; set; }
    public string ExportPath { get; set; }
    public ExportResult Result { get; set; }
}
```

#### User Preferences
```csharp
public class UserPreferences
{
    public AppFilterType DefaultFilter { get; set; }
    public bool AutoLoadOnDeviceConnect { get; set; }
    public string DefaultExportPath { get; set; }
    public ExportTemplate DefaultExportTemplate { get; set; }
}
```

### 7. Advanced Error Handling

#### Retry Policies
```csharp
public class RetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    public double BackoffMultiplier { get; set; } = 2.0;
    public Func<Exception, bool> ShouldRetry { get; set; }
}
```

#### Health Monitoring
```csharp
public class ModuleHealthMonitor
{
    public async Task<HealthStatus> CheckDeviceConnectionAsync();
    public async Task<HealthStatus> CheckAdbVersionAsync();
    public async Task<HealthStatus> CheckStorageSpaceAsync();
}
```

## Kết Luận

Module "Installed Apps" được thiết kế với kiến trúc MVVM rõ ràng, tách biệt concerns và dễ maintain. Code được tối ưu cho performance và UX, với comprehensive error handling và progress tracking.

Các cải tiến đề xuất sẽ nâng cao khả năng của module về:
- **Performance**: Caching, background processing, virtualization
- **Functionality**: Advanced filtering, batch operations, external integrations
- **Usability**: Customizable UI, saved preferences, export templates
- **Reliability**: Better error handling, health monitoring, retry policies

Module có thể được mở rộng dễ dàng nhờ architecture linh hoạt và separation of concerns tốt.
