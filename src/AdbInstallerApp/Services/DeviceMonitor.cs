using AdbInstallerApp.Models;


namespace AdbInstallerApp.Services
{
    public class DeviceMonitor : IDisposable
    {
        private readonly AdbService _adb;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(2);
        private CancellationTokenSource? _cts;


        public event Action<List<DeviceInfo>>? DevicesChanged;


        public DeviceMonitor(AdbService adb)
        {
            _adb = adb;
        }


        public void Start()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                var last = new Dictionary<string, string>();
                while (!_cts!.IsCancellationRequested)
                {
                    try
                    {
                        var list = await _adb.ListDevicesAsync();
                        var dict = list.ToDictionary(d => d.Serial, d => d.State);
                        if (!AreSame(dict, last))
                        {
                            last = dict;
                            DevicesChanged?.Invoke(list);
                        }
                    }
                    catch { }
                    await Task.Delay(_interval, _cts.Token);
                }
            });
        }


        private static bool AreSame(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var kv in a)
            {
                if (!b.TryGetValue(kv.Key, out var v) || v != kv.Value) return false;
            }
            return true;
        }


        public void Dispose() => _cts?.Cancel();
    }
}