namespace AdbInstallerApp.Models
{
    /// <summary>
    /// Result of installing a single APK
    /// </summary>
    public class ApkInstallResult
    {
        public string ApkPath { get; }
        public bool Success { get; }
        public string Message { get; }
        public string DeviceSerial { get; }
        public DateTime CompletedAt { get; }

        public ApkInstallResult(string apkPath, bool success, string message, string deviceSerial = "")
        {
            ApkPath = apkPath;
            Success = success;
            Message = message;
            DeviceSerial = deviceSerial;
            CompletedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Result of installing a group of APKs
    /// </summary>
    public class GroupInstallResult
    {
        public string GroupName { get; }
        public bool Success { get; }
        public string Message { get; }
        public List<ApkInstallResult> ApkResults { get; }
        public DateTime CompletedAt { get; }
        public int TotalApks => ApkResults.Count;
        public int SuccessfulApks => ApkResults.Count(r => r.Success);

        public GroupInstallResult(string groupName, bool success, string message, List<ApkInstallResult> apkResults)
        {
            GroupName = groupName;
            Success = success;
            Message = message;
            ApkResults = apkResults ?? new List<ApkInstallResult>();
            CompletedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Result of installing multiple groups
    /// </summary>
    public class MultiGroupInstallResult
    {
        public bool Success { get; }
        public string Message { get; }
        public List<GroupInstallResult> GroupResults { get; }
        public DateTime CompletedAt { get; }
        public int TotalGroups => GroupResults.Count;
        public int SuccessfulGroups => GroupResults.Count(r => r.Success);
        public int TotalApks => GroupResults.Sum(g => g.TotalApks);
        public int SuccessfulApks => GroupResults.Sum(g => g.SuccessfulApks);

        public MultiGroupInstallResult(bool success, string message, List<GroupInstallResult> groupResults)
        {
            Success = success;
            Message = message;
            GroupResults = groupResults ?? new List<GroupInstallResult>();
            CompletedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Progress information for installation tracking
    /// </summary>
    public class InstallProgressInfo
    {
        public double Progress { get; }
        public string Message { get; }
        public string CurrentGroup { get; }
        public string CurrentApk { get; }
        public InstallStatus Status { get; }
        public DateTime Timestamp { get; }

        public InstallProgressInfo(double progress, string message, string currentGroup = "", InstallStatus status = InstallStatus.InProgress, string currentApk = "")
        {
            Progress = Math.Max(0, Math.Min(100, progress));
            Message = message ?? "";
            CurrentGroup = currentGroup ?? "";
            CurrentApk = currentApk ?? "";
            Status = status;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Installation task for a group of APKs
    /// </summary>
    public class GroupInstallTask
    {
        public string GroupName { get; }
        public List<DeviceInfo> TargetDevices { get; }
        public List<ApkItem> ApkFiles { get; }
        public InstallOptions InstallOptions { get; }

        public GroupInstallTask(string groupName, List<DeviceInfo> targetDevices, List<ApkItem> apkFiles, InstallOptions installOptions)
        {
            GroupName = groupName ?? throw new ArgumentNullException(nameof(groupName));
            TargetDevices = targetDevices ?? throw new ArgumentNullException(nameof(targetDevices));
            ApkFiles = apkFiles ?? throw new ArgumentNullException(nameof(apkFiles));
            InstallOptions = installOptions ?? throw new ArgumentNullException(nameof(installOptions));
        }
    }

    /// <summary>
    /// Installation options
    /// </summary>
    public class InstallOptions
    {
        public bool Reinstall { get; }
        public bool GrantPermissions { get; }
        public bool AllowDowngrade { get; }
        public int MaxRetries { get; }

        public InstallOptions(bool reinstall = false, bool grantPermissions = false, bool allowDowngrade = false, int maxRetries = 2)
        {
            Reinstall = reinstall;
            GrantPermissions = grantPermissions;
            AllowDowngrade = allowDowngrade;
            MaxRetries = maxRetries;
        }

        public string ToAdbOptions()
        {
            var options = new List<string>();
            if (Reinstall) options.Add("-r");
            if (GrantPermissions) options.Add("-g");
            if (AllowDowngrade) options.Add("-d");
            return string.Join(" ", options);
        }
    }

    /// <summary>
    /// Installation status enumeration
    /// </summary>
    public enum InstallStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed,
        Cancelled
    }
}
