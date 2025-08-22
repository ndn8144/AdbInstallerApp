using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace AdbInstallerApp.ViewModels
{
    public partial class InstallationProgressViewModel : ObservableObject
    {
        [ObservableProperty] 
        private int _currentDevice = 1;
        
        [ObservableProperty] 
        private int _totalDevices = 1;
        
        [ObservableProperty] 
        private int _currentApk = 1;
        
        [ObservableProperty] 
        private int _totalApks = 1;
        
        [ObservableProperty] 
        private string _currentStatus = "Preparing installation...";
        
        [ObservableProperty] 
        private double _overallProgress = 0.0;
        
        [ObservableProperty]
        private bool _isIndeterminate = false;
        
        [ObservableProperty]
        private string _currentDeviceName = "";
        
        [ObservableProperty]
        private string _currentApkName = "";

        public string ProgressText => $"Device {CurrentDevice}/{TotalDevices} - APK {CurrentApk}/{TotalApks}";
        
        public string DeviceProgressText => $"Installing on {CurrentDeviceName}";
        
        public string ApkProgressText => $"Installing {CurrentApkName}";
        
        public ObservableCollection<InstallationLogEntry> LogEntries { get; } = new();
        
        public void UpdateProgress(int device, int totalDevices, int apk, int totalApks, double progress)
        {
            CurrentDevice = device;
            TotalDevices = totalDevices;
            CurrentApk = apk;
            TotalApks = totalApks;
            OverallProgress = progress;
            IsIndeterminate = false;
        }
        
        public void SetIndeterminate(bool indeterminate)
        {
            IsIndeterminate = indeterminate;
        }
        
        public void AddLogEntry(string deviceSerial, string apkName, InstallStatus status, string message, TimeSpan? duration = null)
        {
            var entry = new InstallationLogEntry
            {
                Timestamp = DateTime.Now,
                DeviceSerial = deviceSerial,
                ApkName = apkName,
                Status = status,
                Message = message,
                Duration = duration ?? TimeSpan.Zero
            };
            
            LogEntries.Add(entry);
        }
        
        public void ClearLog()
        {
            LogEntries.Clear();
        }
    }
    
    public class InstallationLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string DeviceSerial { get; set; } = string.Empty;
        public string ApkName { get; set; } = string.Empty;
        public InstallStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        
        public string StatusIcon => Status switch
        {
            InstallStatus.Success => "✅",
            InstallStatus.Failed => "❌",
            InstallStatus.InProgress => "⏳",
            InstallStatus.Skipped => "⏭️",
            _ => "❓"
        };
        
        public string DurationFormatted => Duration.TotalSeconds > 0 ? $"{Duration.TotalSeconds:F1}s" : "";
    }
    
    public enum InstallStatus
    {
        InProgress,
        Success,
        Failed,
        Skipped
    }
}
