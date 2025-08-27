using AdbInstallerApp.Models;


namespace AdbInstallerApp.Services
{
public class InstallOrchestrator
{
private readonly AdbService _adb;
public InstallOrchestrator(AdbService adb) { _adb = adb; }


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
}
}