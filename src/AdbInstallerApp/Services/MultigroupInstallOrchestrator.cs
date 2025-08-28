using AdbInstallerApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.IO;
using System.Collections.Concurrent;

namespace AdbInstallerApp.Services
{
    /// <summary>
    /// Orchestrator cho việc cài đặt nhiều group APK lần lượt với progress tracking và error handling
    /// </summary>
    public sealed class MultiGroupInstallOrchestrator : INotifyPropertyChanged
    {
        private readonly AdbService _adbService;
        private readonly InstallOrchestrator _singleInstaller;
        private CancellationTokenSource? _cancellationTokenSource;
        
        // Progress tracking properties
        public double OverallProgress { get; private set; }
        public string CurrentStatus { get; private set; } = "Ready";
        public string CurrentGroup { get; private set; } = "";
        public string CurrentApk { get; private set; } = "";
        public bool IsInstalling { get; private set; }
        
        // Installation queue and results
        private readonly ConcurrentQueue<GroupInstallTask> _installQueue = new();
        private readonly List<GroupInstallResult> _results = new();

        public MultiGroupInstallOrchestrator(AdbService adbService, InstallOrchestrator singleInstaller)
        {
            _adbService = adbService ?? throw new ArgumentNullException(nameof(adbService));
            _singleInstaller = singleInstaller ?? throw new ArgumentNullException(nameof(singleInstaller));
        }

        /// <summary>
        /// Bắt đầu cài đặt nhiều group APK lần lượt
        /// </summary>
        /// <param name="installTasks">Danh sách các group cần cài đặt</param>
        /// <param name="progressCallback">Callback báo cáo tiến độ</param>
        /// <param name="cancellationToken">Token để cancel operation</param>
        public async Task<MultiGroupInstallResult> InstallGroupsSequentiallyAsync(
            IEnumerable<GroupInstallTask> installTasks,
            IProgress<InstallProgressInfo>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            var tasks = installTasks.ToList();
            if (tasks.Count == 0)
                return new MultiGroupInstallResult(true, "No groups to install", new List<GroupInstallResult>());

            try
            {
                IsInstalling = true;
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _results.Clear();

                // Enqueue all tasks
                foreach (var task in tasks)
                {
                    _installQueue.Enqueue(task);
                }

                UpdateStatus("Starting multi-group installation...");
                progressCallback?.Report(new InstallProgressInfo(0, "Initializing installation queue..."));

                var totalGroups = tasks.Count;
                var completedGroups = 0;

                // Process each group sequentially
                while (_installQueue.TryDequeue(out var currentTask))
                {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    CurrentGroup = currentTask.GroupName;
                    UpdateStatus($"Installing group: {currentTask.GroupName}");

                    try
                    {
                        // Install current group
                        var groupResult = await InstallSingleGroupAsync(
                            currentTask, 
                            CreateGroupProgressCallback(progressCallback, completedGroups, totalGroups),
                            _cancellationTokenSource.Token);

                        _results.Add(groupResult);
                        completedGroups++;

                        // Update overall progress
                        OverallProgress = (double)completedGroups / totalGroups * 100;
                        
                        var statusMessage = groupResult.Success 
                            ? $"✅ Completed group: {currentTask.GroupName}"
                            : $"❌ Failed group: {currentTask.GroupName} - {groupResult.Message}";
                        
                        progressCallback?.Report(new InstallProgressInfo(
                            OverallProgress, 
                            statusMessage, 
                            currentTask.GroupName,
                            groupResult.Success ? InstallStatus.Completed : InstallStatus.Failed));

                        // Delay between groups để tránh overload device
                        if (_installQueue.Count > 0) // Còn group khác
                        {
                            UpdateStatus("Waiting before next group...");
                            await Task.Delay(2000, _cancellationTokenSource.Token); // 2 second delay
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        var cancelledResult = new GroupInstallResult(
                            currentTask.GroupName, 
                            false, 
                            "Installation cancelled by user",
                            new List<ApkInstallResult>());
                        _results.Add(cancelledResult);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        var errorResult = new GroupInstallResult(
                            currentTask.GroupName,
                            false,
                            $"Unexpected error: {ex.Message}",
                            new List<ApkInstallResult>());
                        _results.Add(errorResult);
                        
                        // Continue with next group nếu không phải critical error
                        if (!IsCriticalError(ex))
                        {
                            completedGroups++;
                            continue;
                        }
                        throw;
                    }
                }

                // Tính toán kết quả tổng thể
                var successfulGroups = _results.Count(r => r.Success);
                var overallSuccess = successfulGroups == totalGroups;
                var summaryMessage = $"Installation completed: {successfulGroups}/{totalGroups} groups successful";

                UpdateStatus(overallSuccess ? "✅ All groups installed successfully!" : "⚠️ Some groups failed to install");
                
                return new MultiGroupInstallResult(overallSuccess, summaryMessage, _results.ToList());
            }
            finally
            {
                IsInstalling = false;
                CurrentGroup = "";
                CurrentApk = "";
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Cài đặt một group APK với detailed progress tracking
        /// </summary>
        private async Task<GroupInstallResult> InstallSingleGroupAsync(
            GroupInstallTask groupTask,
            IProgress<InstallProgressInfo> progressCallback,
            CancellationToken cancellationToken)
        {
            var apkResults = new List<ApkInstallResult>();
            var totalApks = groupTask.ApkFiles.Count;
            var completedApks = 0;

            progressCallback.Report(new InstallProgressInfo(
                0, 
                $"Starting installation of {totalApks} APKs in group: {groupTask.GroupName}",
                groupTask.GroupName,
                InstallStatus.InProgress));

            foreach (var apkFile in groupTask.ApkFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                CurrentApk = Path.GetFileName(apkFile.FilePath);
                
                try
                {
                    progressCallback.Report(new InstallProgressInfo(
                        (double)completedApks / totalApks * 100,
                        $"Installing: {CurrentApk}",
                        groupTask.GroupName,
                        InstallStatus.InProgress,
                        CurrentApk));

                    // Cài đặt APK với retry logic
                    var installResult = await InstallApkWithRetryAsync(
                        groupTask.TargetDevices,
                        apkFile,
                        groupTask.InstallOptions,
                        cancellationToken);

                    apkResults.Add(installResult);
                    completedApks++;

                    // Report progress cho APK vừa hoàn thành
                    var apkProgress = (double)completedApks / totalApks * 100;
                    var statusMessage = installResult.Success 
                        ? $"✅ Installed: {CurrentApk}"
                        : $"❌ Failed: {CurrentApk} - {installResult.Message}";

                    progressCallback.Report(new InstallProgressInfo(
                        apkProgress,
                        statusMessage,
                        groupTask.GroupName,
                        installResult.Success ? InstallStatus.Completed : InstallStatus.Failed,
                        CurrentApk));

                    // Short delay between APKs để device có thể process
                    if (completedApks < totalApks)
                    {
                        await Task.Delay(500, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    apkResults.Add(new ApkInstallResult(apkFile.FilePath, false, "Cancelled by user"));
                    throw;
                }
                catch (Exception ex)
                {
                    var errorResult = new ApkInstallResult(apkFile.FilePath, false, ex.Message);
                    apkResults.Add(errorResult);
                    
                    // Continue với APK tiếp theo
                    completedApks++;
                    progressCallback.Report(new InstallProgressInfo(
                        (double)completedApks / totalApks * 100,
                        $"❌ Error installing {CurrentApk}: {ex.Message}",
                        groupTask.GroupName,
                        InstallStatus.Failed,
                        CurrentApk));
                }
            }

            // Tính toán kết quả cho group
            var successfulApks = apkResults.Count(r => r.Success);
            var groupSuccess = successfulApks > 0; // Ít nhất 1 APK thành công
            var groupMessage = $"Group {groupTask.GroupName}: {successfulApks}/{totalApks} APKs installed successfully";

            return new GroupInstallResult(groupTask.GroupName, groupSuccess, groupMessage, apkResults);
        }

        /// <summary>
        /// Cài đặt APK với retry logic để handle temporary failures
        /// </summary>
        private async Task<ApkInstallResult> InstallApkWithRetryAsync(
            List<DeviceInfo> devices,
            ApkItem apkFile,
            InstallOptions options,
            CancellationToken cancellationToken,
            int maxRetries = 2)
        {
            Exception? lastException = null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (attempt > 0)
                    {
                        // Wait before retry
                        await Task.Delay(1000 * attempt, cancellationToken);
                    }

                    // Sử dụng existing InstallOrchestrator cho single APK installation
                    await _singleInstaller.InstallAsync(
                        devices,
                        new List<ApkItem> { apkFile },
                        options.Reinstall,
                        options.GrantPermissions,
                        options.AllowDowngrade,
                        message => { /* Log if needed */ });

                    return new ApkInstallResult(apkFile.FilePath, true, "Installation successful");
                }
                catch (OperationCanceledException)
                {
                    throw; // Don't retry cancellation
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    // Don't retry certain types of errors
                    if (IsNonRetryableError(ex))
                    {
                        break;
                    }
                }
            }

            return new ApkInstallResult(
                apkFile.FilePath, 
                false, 
                $"Failed after {maxRetries + 1} attempts: {lastException?.Message ?? "Unknown error"}");
        }

        /// <summary>
        /// Tạo progress callback cho group installation
        /// </summary>
        private IProgress<InstallProgressInfo> CreateGroupProgressCallback(
            IProgress<InstallProgressInfo>? mainCallback,
            int completedGroups,
            int totalGroups)
        {
            return new Progress<InstallProgressInfo>(info =>
            {
                // Calculate overall progress: completed groups + current group progress
                var groupWeight = 100.0 / totalGroups;
                var overallProgress = (completedGroups * groupWeight) + (info.Progress * groupWeight / 100);
                
                OverallProgress = Math.Min(100, overallProgress);
                
                // Forward to main callback with adjusted progress
                mainCallback?.Report(new InstallProgressInfo(
                    OverallProgress,
                    info.Message,
                    info.CurrentGroup,
                    info.Status,
                    info.CurrentApk));
            });
        }

        /// <summary>
        /// Hủy bỏ installation đang chạy
        /// </summary>
        public void CancelInstallation()
        {
            _cancellationTokenSource?.Cancel();
            UpdateStatus("Cancelling installation...");
        }

        /// <summary>
        /// Check xem error có phải loại không thể retry không
        /// </summary>
        private static bool IsNonRetryableError(Exception ex)
        {
            return ex is ArgumentException || 
                   ex is FileNotFoundException ||
                   ex is UnauthorizedAccessException ||
                   ex.Message.Contains("INSTALL_FAILED_INVALID_APK");
        }

        /// <summary>
        /// Check xem error có phải critical error cần stop toàn bộ process không
        /// </summary>
        private static bool IsCriticalError(Exception ex)
        {
            return ex is OutOfMemoryException ||
                   ex is StackOverflowException ||
                   ex.Message.Contains("device disconnected");
        }

        private void UpdateStatus(string status)
        {
            CurrentStatus = status;
            OnPropertyChanged(nameof(CurrentStatus));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
