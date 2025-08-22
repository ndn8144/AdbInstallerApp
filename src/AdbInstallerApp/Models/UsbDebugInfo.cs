using System.Collections.Generic;

namespace AdbInstallerApp.Models
{
    public enum UsbDebugStatus
    {
        Unknown,
        Ready,
        NeedAuthorize,
        Offline
    }

    public class UsbDebugInfo
    {
        public UsbDebugStatus Status { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<string> Steps { get; set; } = new List<string>();
    }
}
