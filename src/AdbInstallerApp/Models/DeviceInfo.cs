namespace AdbInstallerApp.Models
{
    public class DeviceInfo
    {
        // Basic Device Information (existing)
        public string Serial { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty; // device | unauthorized | offline
        public string Model { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty; // Samsung, Xiaomi, etc.
        public string Product { get; set; } = string.Empty; // Product name
        public string AndroidVersion { get; set; } = string.Empty; // Android version
        public string Sdk { get; set; } = string.Empty; // API level
        public string Abi { get; set; } = string.Empty; // Architecture
        public string Density { get; set; } = string.Empty; // Screen density
        public bool IsRooted { get; set; } = false; // Root status
        public string BuildNumber { get; set; } = string.Empty; // Build number

        // System Build Information (new)
        public string BuildFingerprint { get; set; } = string.Empty;
        public string BuildDate { get; set; } = string.Empty;
        public string BuildUser { get; set; } = string.Empty;
        public string BuildHost { get; set; } = string.Empty;
        public string BuildTags { get; set; } = string.Empty;
        public bool IsDebuggable { get; set; } = false;
        public bool IsSecure { get; set; } = false;
        public string BuildType { get; set; } = string.Empty; // user/userdebug/eng

        // Hardware & Performance (new)
        public string CpuInfo { get; set; } = string.Empty;
        public string MemoryInfo { get; set; } = string.Empty;
        public string SupportedAbis { get; set; } = string.Empty;
        public string SupportedAbis32 { get; set; } = string.Empty;
        public string SupportedAbis64 { get; set; } = string.Empty;
        public string StorageInfo { get; set; } = string.Empty;
        public string PartitionInfo { get; set; } = string.Empty;
        public string BatteryLevel { get; set; } = string.Empty;
        public string BatteryStatus { get; set; } = string.Empty;
        public string BatteryTemperature { get; set; } = string.Empty;
        public bool IsCharging { get; set; } = false;

        // Network & Connectivity (new)
        public string NetworkInterfaces { get; set; } = string.Empty;
        public string WifiInfo { get; set; } = string.Empty;
        public string ConnectivityInfo { get; set; } = string.Empty;
        public string NetworkStats { get; set; } = string.Empty;
        public string WifiSsid { get; set; } = string.Empty;
        public bool IsWifiConnected { get; set; } = false;
        public bool HasMobileData { get; set; } = false;

        // Display & Graphics (new)
        public string ScreenResolution { get; set; } = string.Empty;
        public string ScreenDensity { get; set; } = string.Empty;
        public string DisplayInfo { get; set; } = string.Empty;
        public string LcdDensity { get; set; } = string.Empty;
        public string GraphicsInfo { get; set; } = string.Empty;
        public bool HasHardwareAcceleration { get; set; } = false;

        // Security & Permissions (new)
        public string SelinuxMode { get; set; } = string.Empty;
        public string KernelVersion { get; set; } = string.Empty;
        public string EncryptionState { get; set; } = string.Empty;
        public string DecryptionStatus { get; set; } = string.Empty;
        public string SuBinaryLocation { get; set; } = string.Empty;
        public bool HasSuperuserApp { get; set; } = false;
        public bool HasXbinTools { get; set; } = false;

        // Runtime & Apps (new)
        public int TotalPackages { get; set; } = 0;
        public int ThirdPartyPackages { get; set; } = 0;
        public int SystemPackages { get; set; } = 0;
        public int DisabledPackages { get; set; } = 0;
        public string ProcessInfo { get; set; } = string.Empty;
        public string CpuUsage { get; set; } = string.Empty;
        public string ActivityManagerInfo { get; set; } = string.Empty;

        // Temperature & Sensors (new)
        public string ThermalZones { get; set; } = string.Empty;
        public string SensorInfo { get; set; } = string.Empty;

        // Developer Options Status (new)
        public bool DeveloperOptionsEnabled { get; set; } = false;
        public bool AdbEnabled { get; set; } = false;
        public bool UsbDebuggingEnabled { get; set; } = false;
        public bool VerifyAppsOverUsb { get; set; } = false;

        // Troubleshooting Status (new)
        public bool UsbConnectionOk { get; set; } = false;
        public bool DriverInstalled { get; set; } = false;
        public bool AdbAuthorized { get; set; } = false;
        public bool DeviceCompatible { get; set; } = false;
        public string ConnectionDiagnosis { get; set; } = string.Empty;

        // Computed Properties
        public string CreatedAtText => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        public string LastUpdatedText => DateTime.Now.ToString("HH:mm:ss");
        
        // Status Indicators
        public string ConnectionStatusIcon => State switch
        {
            "device" => "🟢",
            "unauthorized" => "🟡", 
            "offline" => "🔴",
            _ => "⚪"
        };

        public string RootStatusIcon => IsRooted ? "🔓" : "🔒";
        public string SecurityStatusIcon => IsSecure ? "🛡️" : "⚠️";
        public string BatteryStatusIcon => IsCharging ? "🔌" : "🔋";
    }
}