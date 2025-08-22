namespace AdbInstallerApp.Models
{
public class DeviceInfo
{
public string Serial { get; set; } = string.Empty;
public string State { get; set; } = string.Empty; // device | unauthorized | offline
public string Model { get; set; } = string.Empty;
public string Sdk { get; set; } = string.Empty;
public string Abi { get; set; } = string.Empty;
public string Density { get; set; } = string.Empty;
}
}