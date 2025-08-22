namespace AdbInstallerApp.Models
{
public class DeviceInfo
{
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
}
}