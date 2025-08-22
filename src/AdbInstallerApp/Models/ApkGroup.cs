using System.Collections.Generic;
using System.Linq;

namespace AdbInstallerApp.Models
{
    public class ApkGroup
    {
        public string PackageName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public ApkItem BaseApk { get; set; } = null!;
        public List<ApkItem> SplitApks { get; set; } = new List<ApkItem>();
        public bool HasValidBase => BaseApk != null;
        public long TotalSize => (BaseApk?.FileSize ?? 0) + SplitApks.Sum(s => s.FileSize);
        
        // Convenience properties
        public string VersionName => BaseApk?.VersionName ?? "";
        public string VersionCode => BaseApk?.VersionCode ?? "";
        public string TargetSdkVersion => BaseApk?.TargetSdkVersion ?? "";
        public bool IsSplit => SplitApks.Count > 0;
        
        // Grouped splits by type
        public List<ApkItem> AbiSplits => SplitApks.Where(s => s.Type == ApkType.SplitAbi).ToList();
        public List<ApkItem> DpiSplits => SplitApks.Where(s => s.Type == ApkType.SplitDpi).ToList();
        public List<ApkItem> LanguageSplits => SplitApks.Where(s => s.Type == ApkType.SplitLanguage).ToList();
        public List<ApkItem> OtherSplits => SplitApks.Where(s => s.Type == ApkType.SplitOther).ToList();
        
        // Display info
        public string DisplayInfo => $"{DisplayName} v{VersionName} ({VersionCode})";
        public string SplitInfo => IsSplit ? $"{SplitApks.Count} splits" : "Single APK";
        public string SizeInfo => FormatFileSize(TotalSize);
        
        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
            return $"{bytes / (1024 * 1024 * 1024):F1} GB";
        }
    }
}
