namespace AdbInstallerApp.Models
{
    public sealed record InstalledApp(
        string PackageName,
        string Label,
        string VersionName,
        long VersionCode,
        int UserId,
        bool IsSystemApp,
        IReadOnlyList<string> CodePaths,   // base + splits (device paths)
        long? TotalSizeBytes               // optional
    )
    {
        public bool HasSplits => CodePaths.Count > 1;
        public string DisplayName => !string.IsNullOrEmpty(Label) ? Label : PackageName;
        public string VersionInfo => !string.IsNullOrEmpty(VersionName) ? $"{VersionName} ({VersionCode})" : VersionCode.ToString();
        public string AppType => IsSystemApp ? "System" : "User";
        public string SizeInfo => TotalSizeBytes?.ToString("N0") + " bytes" ?? "Unknown";
        public string SplitInfo => HasSplits ? $"Split ({CodePaths.Count})" : "Single";
    }

    public sealed record ExportResult(
        string PackageName,
        IReadOnlyList<string> ExportedPaths,
        bool Success = true,
        string? ErrorMessage = null
    );

    public sealed record TransferProgress(
        string RemotePath,
        string LocalPath,
        long BytesTransferred,
        long TotalBytes,
        double ProgressPercent
    )
    {
        public static TransferProgress FromAdb(string adbOutput, string remotePath, string localPath)
        {
            // Parse adb pull progress output
            // Example: "[  50%] /data/app/com.example/base.apk"
            var lines = adbOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var lastProgressLine = lines.LastOrDefault(l => l.Contains('%'));
            
            if (lastProgressLine != null && lastProgressLine.Contains('[') && lastProgressLine.Contains('%'))
            {
                var percentStart = lastProgressLine.IndexOf('[') + 1;
                var percentEnd = lastProgressLine.IndexOf('%');
                if (percentEnd > percentStart)
                {
                    var percentStr = lastProgressLine.Substring(percentStart, percentEnd - percentStart).Trim();
                    if (double.TryParse(percentStr, out var percent))
                    {
                        return new TransferProgress(remotePath, localPath, 0, 100, percent);
                    }
                }
            }
            
            return new TransferProgress(remotePath, localPath, 0, 100, 0);
        }
    }

    public class AppQueryOptions
    {
        public int UserId { get; set; } = 0;
        public bool OnlyUserApps { get; set; } = false;
        public bool IncludeSystemApps { get; set; } = true;
        public string? KeywordFilter { get; set; }
        public bool WithSizes { get; set; } = false;
    }
}