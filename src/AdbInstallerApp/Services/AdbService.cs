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
break;
case "[ro.build.display.id]":
device.BuildNumber = value;
break;
}
}
}
}

// Check if device is rooted
var (rootCode, rootOutput, _) = await ProcessRunner.RunAsync(_adbPath, $"-s {device.Serial} shell which su");
device.IsRooted = rootCode == 0 && !string.IsNullOrWhiteSpace(rootOutput.Trim());
}
}
catch
{
// Ignore errors, use default values
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