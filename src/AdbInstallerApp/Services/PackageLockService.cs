using System.Collections.Concurrent;

namespace AdbInstallerApp.Services
{
    public interface IPackageLockService
    {
        Task<IDisposable> AcquireLockAsync(string deviceSerial, string packageName, CancellationToken ct = default);
        bool IsLocked(string deviceSerial, string packageName);
    }

    public sealed class PackageLockService : IPackageLockService
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private readonly ConcurrentDictionary<string, string> _activeLocks = new();

        public async Task<IDisposable> AcquireLockAsync(string deviceSerial, string packageName, CancellationToken ct = default)
        {
            var lockKey = GetLockKey(deviceSerial, packageName);
            var semaphore = _locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            _activeLocks[lockKey] = $"{deviceSerial}:{packageName}";

            return new PackageLock(this, lockKey, semaphore);
        }

        public bool IsLocked(string deviceSerial, string packageName)
        {
            var lockKey = GetLockKey(deviceSerial, packageName);
            return _activeLocks.ContainsKey(lockKey);
        }

        private void ReleaseLock(string lockKey, SemaphoreSlim semaphore)
        {
            _activeLocks.TryRemove(lockKey, out _);
            semaphore.Release();
        }

        private static string GetLockKey(string deviceSerial, string packageName)
        {
            return $"{deviceSerial}:{packageName}";
        }

        private sealed class PackageLock : IDisposable
        {
            private readonly PackageLockService _service;
            private readonly string _lockKey;
            private readonly SemaphoreSlim _semaphore;
            private bool _disposed;

            public PackageLock(PackageLockService service, string lockKey, SemaphoreSlim semaphore)
            {
                _service = service;
                _lockKey = lockKey;
                _semaphore = semaphore;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _service.ReleaseLock(_lockKey, _semaphore);
            }
        }
    }
}
