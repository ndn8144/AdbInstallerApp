using AdbInstallerApp.Models;
using System.Linq;

namespace AdbInstallerApp.Services
{
public class InstallOrchestrator
{
private readonly AdbService _adb;
private readonly DeviceCompatibilityAnalyzer _compatibilityAnalyzer;

public InstallOrchestrator(AdbService adb) 
{ 
    _adb = adb; 
    _compatibilityAnalyzer = new DeviceCompatibilityAnalyzer();
}


public async Task InstallAsync(
IEnumerable<DeviceInfo> devices,
IEnumerable<ApkItem> apks,
bool reinstall, bool grant, bool downgrade,
Action<string>? log = null)
{
var apkList = apks.ToList();
if (apkList.Count == 0) { log?.Invoke("[INFO] No APK selected."); return; }


var opts = BuildOpts(reinstall, grant, downgrade);


int i = 0; int n = devices.Count();
foreach (var d in devices)
{
i++;
log?.Invoke($"[${i}/${n}] Installing on {d.Serial} ({d.State})...");


if (!string.Equals(d.State, "device", StringComparison.OrdinalIgnoreCase))
{
log?.Invoke($"[SKIP] {d.Serial} is not in 'device' state.");
continue;
}


(bool ok, string logText) result;
if (apkList.Count == 1)
{
result = await _adb.InstallSingleAsync(d.Serial, apkList[0].FilePath, opts);
}
else
{
result = await _adb.InstallMultipleAsync(d.Serial, apkList.Select(a => a.FilePath), opts);
}


log?.Invoke(result.logText.Trim());
log?.Invoke(result.ok ? $"[OK] {d.Serial} finished." : $"[FAIL] {d.Serial} failed.");
log?.Invoke("");
}
}


private static string BuildOpts(bool reinstall, bool grant, bool downgrade)
{
var list = new List<string>();
if (reinstall) list.Add("-r");
if (grant) list.Add("-g");
if (downgrade) list.Add("-d");
return string.Join(' ', list);
}

// New method for installing APK groups with compatibility analysis
public async Task InstallGroupsAsync(
IEnumerable<DeviceInfo> devices,
IEnumerable<ApkGroup> apkGroups,
bool reinstall, bool grant, bool downgrade,
Action<string>? log = null)
{
var groupsList = apkGroups.ToList();
if (groupsList.Count == 0) { log?.Invoke("[INFO] No APK groups selected."); return; }

var opts = BuildOpts(reinstall, grant, downgrade);

int i = 0; int n = devices.Count();
foreach (var device in devices)
{
i++;
log?.Invoke($"[{i}/{n}] Installing on {device.Serial} ({device.State})...");

if (!string.Equals(device.State, "device", StringComparison.OrdinalIgnoreCase))
{
log?.Invoke($"[SKIP] {device.Serial} is not in 'device' state.");
continue;
}

foreach (var group in groupsList)
{
log?.Invoke($"Installing {group.DisplayName}...");

// Auto-select optimal splits for device
var compatibility = _compatibilityAnalyzer.CheckCompatibility(device, group);
var apksToInstall = BuildInstallList(group, compatibility, opts);

if (apksToInstall.Count == 1)
{
var result = await _adb.InstallSingleAsync(device.Serial, apksToInstall[0], opts);
log?.Invoke(result.ok ? "[OK] Single APK installed" : $"[FAIL] {result.log}");
}
else
{
var result = await _adb.InstallMultipleAsync(device.Serial, apksToInstall, opts);
log?.Invoke(result.log.Trim());
log?.Invoke(result.ok ? $"[OK] {apksToInstall.Count} APKs installed" : $"[FAIL] {result.log}");
}
}

log?.Invoke($"[OK] {device.Serial} finished.");
log?.Invoke("");
}
}

private List<string> BuildInstallList(ApkGroup group, CompatibilityResult compatibility, string options)
{
var apksToInstall = new List<string>();

// Always include base APK
if (group.BaseApk != null)
apksToInstall.Add(group.BaseApk.FilePath);

// Add recommended splits based on compatibility
if (compatibility.RecommendedSplits.Count > 0)
apksToInstall.AddRange(compatibility.RecommendedSplits.Select(s => s.FilePath));

// Add optimal DPI split if available
if (compatibility.OptimalDpi != null && !apksToInstall.Contains(compatibility.OptimalDpi.FilePath))
apksToInstall.Add(compatibility.OptimalDpi.FilePath);

return apksToInstall;
}
}
}