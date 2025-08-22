using AdbInstallerApp.Models;
using System.Collections.Concurrent;

namespace AdbInstallerApp.Services
{
    public class DeviceMonitor : IDisposable
    {
        private readonly AdbService _adb;
        private readonly TimeSpan _fastInterval = TimeSpan.FromMilliseconds(500); // Faster polling
        private readonly TimeSpan _slowInterval = TimeSpan.FromSeconds(2); // Slower polling when stable
        private CancellationTokenSource? _cts;
        private bool _hasDevices = false;

        public event Action<List<DeviceInfo>>? DevicesChanged;
        public event Action<DeviceInfo>? DeviceConnected;
        public event Action<DeviceInfo>? DeviceDisconnected;
        public event Action<DeviceInfo>? DeviceStateChanged;

        public DeviceMonitor(AdbService adb)
        {
            _adb = adb;
        }

        public void Start()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _ = Task.Run(MonitorLoop, _cts.Token);
        }

        private async Task MonitorLoop()
        {
            var lastDevices = new ConcurrentDictionary<string, DeviceInfo>();
            var currentInterval = _fastInterval; // Start with fast polling
            var stabilityTimer = 0;

            while (!_cts!.IsCancellationRequested)
            {
                try
                {
                    var currentDevices = await _adb.ListDevicesAsync();
                    var currentDict = currentDevices.ToDictionary(d => d.Serial, d => d);

                    // Check for new devices
                    foreach (var device in currentDevices)
                    {
                        if (!lastDevices.TryGetValue(device.Serial, out var lastDevice))
                        {
                            // New device connected
                            lastDevices[device.Serial] = device;
                            DeviceConnected?.Invoke(device);
                            currentInterval = _fastInterval; // Speed up polling
                            stabilityTimer = 0;
                        }
                        else if (lastDevice.State != device.State)
                        {
                            // Device state changed
                            lastDevices[device.Serial] = device;
                            DeviceStateChanged?.Invoke(device);
                            currentInterval = _fastInterval; // Speed up polling
                            stabilityTimer = 0;
                        }
                    }

                    // Check for disconnected devices
                    var disconnectedDevices = lastDevices.Keys
                        .Where(serial => !currentDict.ContainsKey(serial))
                        .ToList();

                    foreach (var serial in disconnectedDevices)
                    {
                        if (lastDevices.TryRemove(serial, out var device))
                        {
                            DeviceDisconnected?.Invoke(device);
                            currentInterval = _fastInterval; // Speed up polling
                            stabilityTimer = 0;
                        }
                    }

                    // Always notify DevicesChanged for real-time updates
                    DevicesChanged?.Invoke(currentDevices);

                    // Adjust polling interval based on activity
                    if (currentDevices.Count > 0 && !_hasDevices)
                    {
                        _hasDevices = true;
                        currentInterval = _fastInterval;
                        stabilityTimer = 0;
                    }
                    else if (currentDevices.Count == 0 && _hasDevices)
                    {
                        _hasDevices = false;
                        currentInterval = _slowInterval;
                        stabilityTimer = 0;
                    }
                    else if (currentDevices.Count > 0 && currentInterval == _fastInterval)
                    {
                        stabilityTimer++;
                        // After 10 fast polls (5 seconds), slow down polling
                        if (stabilityTimer >= 10)
                        {
                            currentInterval = _slowInterval;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue monitoring
                    System.Diagnostics.Debug.WriteLine($"DeviceMonitor error: {ex.Message}");
                }

                await Task.Delay(currentInterval, _cts.Token);
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}