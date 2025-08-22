using System.Collections.Generic;

namespace AdbInstallerApp.Models
{
    public class ApkManifest
    {
        public string PackageName { get; set; } = string.Empty;
        public string VersionName { get; set; } = string.Empty;
        public string VersionCode { get; set; } = string.Empty;
        public string AppLabel { get; set; } = string.Empty;
        public string TargetSdk { get; set; } = string.Empty;
        public string MinSdk { get; set; } = string.Empty;
        public List<string> SupportedAbis { get; set; } = new List<string>();
        public List<string> Permissions { get; set; } = new List<string>();
        public List<string> Features { get; set; } = new List<string>();
        public bool IsSplit { get; set; } = false;
        public string SplitName { get; set; } = string.Empty;
        public string SplitType { get; set; } = string.Empty;
        public string NativeCode { get; set; } = string.Empty;
        public string ScreenDensity { get; set; } = string.Empty;
        public string ScreenSize { get; set; } = string.Empty;
        public string Locale { get; set; } = string.Empty;
    }

    public enum ApkType
    {
        Base,
        SplitAbi,
        SplitDpi,
        SplitLanguage,
        SplitOther
    }
}
