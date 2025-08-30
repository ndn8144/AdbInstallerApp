using System.Diagnostics;
using System.Text;

namespace AdbInstallerApp.Utils
{
    public sealed class ProcResult
    {
        public int ExitCode { get; set; }
        public string StdOut { get; set; } = "";
        public string StdErr { get; set; } = "";
    }

    public static class Proc
    {
        public static async Task<ProcResult> RunAsync(
            string exe, 
            string args, 
            string? workingDir = null,
            IProgress<string>? log = null, 
            CancellationToken ct = default)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDir ?? Environment.CurrentDirectory
            };

            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var sbOut = new StringBuilder();
            var sbErr = new StringBuilder();

            p.OutputDataReceived += (_, e) => 
            { 
                if (e.Data is { } s) 
                { 
                    sbOut.AppendLine(s); 
                    log?.Report(s); 
                } 
            };
            
            p.ErrorDataReceived += (_, e) => 
            { 
                if (e.Data is { } s) 
                { 
                    sbErr.AppendLine(s); 
                    log?.Report($"ERR: {s}"); 
                } 
            };

            if (!p.Start()) 
                throw new InvalidOperationException($"Cannot start process: {exe}");
            
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            using var reg = ct.Register(() => 
            { 
                try 
                { 
                    if (!p.HasExited) 
                        p.Kill(entireProcessTree: true); 
                } 
                catch { /* Ignore cleanup errors */ } 
            });

            await p.WaitForExitAsync(ct).ConfigureAwait(false);
            
            return new ProcResult 
            { 
                ExitCode = p.ExitCode, 
                StdOut = sbOut.ToString(), 
                StdErr = sbErr.ToString() 
            };
        }

        public static string QuotePath(string path)
        {
            return path.Contains(' ') ? $"\"{path}\"" : path;
        }
    }
}
