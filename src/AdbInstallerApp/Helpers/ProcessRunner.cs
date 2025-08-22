using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;


namespace AdbInstallerApp.Helpers
{
public static class ProcessRunner
{
public static async Task<(int exitCode, string stdout, string stderr)>
RunAsync(string fileName, string arguments, string? workingDir = null, int timeoutMs = 120000)
{
var psi = new ProcessStartInfo(fileName, arguments)
{
UseShellExecute = false,
RedirectStandardOutput = true,
RedirectStandardError = true,
CreateNoWindow = true,
};
if (!string.IsNullOrWhiteSpace(workingDir)) psi.WorkingDirectory = workingDir;


using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
var stdout = new StringBuilder();
var stderr = new StringBuilder();


var tcs = new TaskCompletionSource<int>();


p.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
p.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
p.Exited += (_, __) => tcs.TrySetResult(p.ExitCode);


p.Start();
p.BeginOutputReadLine();
p.BeginErrorReadLine();


var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
if (completed != tcs.Task)
{
try { p.Kill(true); } catch { }
return (-1, stdout.ToString(), "Timeout");
}


return (await tcs.Task, stdout.ToString(), stderr.ToString());
}
}
}