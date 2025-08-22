using AdbInstallerApp.Models;

namespace AdbInstallerApp.Services
{
    public class UsbDebugHelper
    {
        public static class DeviceStates
        {
            public const string Device = "device";
            public const string Unauthorized = "unauthorized"; 
            public const string Offline = "offline";
            public const string NoDevice = "no device";
        }
        
        public UsbDebugInfo AnalyzeDeviceState(DeviceInfo device)
        {
            return device.State switch
            {
                DeviceStates.Unauthorized => new UsbDebugInfo
                {
                    Status = UsbDebugStatus.NeedAuthorize,
                    Title = "⚠️ Chưa được phép",
                    Message = "Thiết bị chưa cho phép USB Debugging",
                    Steps = GetUnauthorizedSteps()
                },
                DeviceStates.Offline => new UsbDebugInfo
                {
                    Status = UsbDebugStatus.Offline,
                    Title = "🔌 Mất kết nối", 
                    Message = "Thiết bị offline hoặc driver có vấn đề",
                    Steps = GetOfflineSteps()
                },
                DeviceStates.Device => new UsbDebugInfo
                {
                    Status = UsbDebugStatus.Ready,
                    Title = "✅ Sẵn sàng",
                    Message = "Thiết bị đã kết nối và sẵn sàng"
                },
                _ => new UsbDebugInfo { Status = UsbDebugStatus.Unknown }
            };
        }

        private List<string> GetUnauthorizedSteps()
        {
            return new List<string>
            {
                "1. Kiểm tra màn hình thiết bị Android",
                "2. Tìm thông báo 'Allow USB Debugging?'",
                "3. Chọn 'Allow' hoặc 'OK'",
                "4. Nếu có, chọn 'Always allow from this computer'",
                "5. Kiểm tra lại trạng thái thiết bị"
            };
        }

        private List<string> GetOfflineSteps()
        {
            return new List<string>
            {
                "1. Kiểm tra cáp USB và cổng kết nối",
                "2. Thử cáp USB khác hoặc cổng khác",
                "3. Kiểm tra driver USB trên máy tính",
                "4. Thử kết nối lại thiết bị",
                "5. Khởi động lại thiết bị Android nếu cần"
            };
        }
    }
}
