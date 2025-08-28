using AdbInstallerApp.Services;
using AdbInstallerApp.Models;
using AdbInstallerApp.ViewModels;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AdbInstallerApp.Examples
{
    /// <summary>
    /// Code mẫu minh họa cách sử dụng CentralizedProgressService
    /// để tích hợp progress bar tập trung vào các operations
    /// </summary>
    public static class ProgressBarIntegrationExample
    {
        #region APK Installation Example

        /// <summary>
        /// Ví dụ: Tích hợp progress bar vào quá trình cài đặt APK
        /// </summary>
        public static async Task InstallApkWithProgressExample()
        {
            var progressService = CentralizedProgressService.Instance;
            
            // 1. Bắt đầu operation
            var operationId = CentralizedProgressService.GenerateOperationId("install");
            progressService.StartOperation(operationId, "Installing APKs", "Preparing installation...");

            try
            {
                // Giả lập danh sách APKs cần cài đặt
                var apkFiles = new[] { "app1.apk", "app2.apk", "app3.apk", "app4.apk", "app5.apk" };
                var totalApks = apkFiles.Length;

                for (int i = 0; i < totalApks; i++)
                {
                    var currentApk = apkFiles[i];
                    
                    // 2. Cập nhật progress cho từng APK
                    var progress = (double)(i + 1) / totalApks * 100;
                    progressService.UpdateProgress(
                        operationId, 
                        progress, 
                        $"Installing {currentApk}...",
                        totalApks, 
                        i + 1
                    );

                    // Giả lập thời gian cài đặt
                    await Task.Delay(2000);
                }

                // 3. Hoàn thành operation
                progressService.CompleteOperation(operationId, "All APKs installed successfully");
            }
            catch (Exception ex)
            {
                // 4. Xử lý lỗi
                progressService.UpdateProgress(operationId, 0, $"Error: {ex.Message}");
                await Task.Delay(2000);
                progressService.CancelOperation(operationId);
            }
        }

        #endregion

        #region Multi-Group Installation Example

        /// <summary>
        /// Ví dụ: Tích hợp vào Multi-Group Installation
        /// </summary>
        public static async Task MultiGroupInstallWithProgressExample()
        {
            var progressService = CentralizedProgressService.Instance;
            var operationId = CentralizedProgressService.GenerateOperationId("multigroup");
            
            progressService.StartOperation(operationId, "Multi-Group Installation", "Starting group installation...");

            try
            {
                // Giả lập các groups cần cài đặt
                var groups = new[] 
                { 
                    new { Name = "Gaming Apps", ApkCount = 3 },
                    new { Name = "Social Media", ApkCount = 5 },
                    new { Name = "Productivity", ApkCount = 4 }
                };

                var totalApks = groups.Sum(g => g.ApkCount);
                var installedApks = 0;

                foreach (var group in groups)
                {
                    // Cập nhật progress cho group hiện tại
                    progressService.UpdateProgress(
                        operationId,
                        (double)installedApks / totalApks * 100,
                        $"Installing group: {group.Name}",
                        totalApks,
                        installedApks
                    );

                    // Giả lập cài đặt từng APK trong group
                    for (int i = 0; i < group.ApkCount; i++)
                    {
                        installedApks++;
                        var progress = (double)installedApks / totalApks * 100;
                        
                        progressService.UpdateProgress(
                            operationId,
                            progress,
                            $"Installing APK {i + 1}/{group.ApkCount} in {group.Name}",
                            totalApks,
                            installedApks
                        );

                        await Task.Delay(1500); // Giả lập thời gian cài đặt
                    }
                }

                progressService.CompleteOperation(operationId, $"Successfully installed {totalApks} APKs in {groups.Length} groups");
            }
            catch (Exception ex)
            {
                progressService.UpdateProgress(operationId, 0, $"Multi-group installation failed: {ex.Message}");
                await Task.Delay(2000);
                progressService.CancelOperation(operationId);
            }
        }

        #endregion

        #region APK Export Example

        /// <summary>
        /// Ví dụ: Tích hợp vào quá trình export APK
        /// </summary>
        public static async Task ExportApkWithProgressExample()
        {
            var progressService = CentralizedProgressService.Instance;
            var operationId = CentralizedProgressService.GenerateOperationId("export");
            
            progressService.StartOperation(operationId, "Exporting APKs", "Scanning installed apps...");

            try
            {
                // Giả lập danh sách apps cần export
                var appsToExport = new[] 
                { 
                    new { Name = "WhatsApp", Size = "50 MB" },
                    new { Name = "Instagram", Size = "120 MB" },
                    new { Name = "Chrome", Size = "80 MB" },
                    new { Name = "YouTube", Size = "95 MB" }
                };

                var totalApps = appsToExport.Length;

                for (int i = 0; i < totalApps; i++)
                {
                    var currentApp = appsToExport[i];
                    var progress = (double)(i + 1) / totalApps * 100;
                    
                    // Cập nhật progress với thông tin chi tiết
                    progressService.UpdateProgress(
                        operationId,
                        progress,
                        $"Exporting {currentApp.Name} ({currentApp.Size})",
                        totalApps,
                        i + 1
                    );

                    // Giả lập thời gian export (apps lớn mất nhiều thời gian hơn)
                    var exportTime = currentApp.Size.Contains("120") ? 3000 : 2000;
                    await Task.Delay(exportTime);
                }

                progressService.CompleteOperation(operationId, $"Exported {totalApps} APKs successfully");
            }
            catch (Exception ex)
            {
                progressService.UpdateProgress(operationId, 0, $"Export failed: {ex.Message}");
                await Task.Delay(2000);
                progressService.CancelOperation(operationId);
            }
        }

        #endregion

        #region Integration with Existing Services

        /// <summary>
        /// Ví dụ: Tích hợp vào InstallOrchestrator hiện có
        /// </summary>
        public static class InstallOrchestratorIntegration
        {
            /// <summary>
            /// Wrapper method cho InstallOrchestrator.InstallAsync với progress tracking
            /// </summary>
            public static async Task InstallWithProgressAsync(
                List<DeviceViewModel> devices, 
                List<ApkItemViewModel> apks, 
                bool reinstall, 
                bool grantPerms, 
                bool allowDowngrade,
                Action<string> logger)
            {
                var progressService = CentralizedProgressService.Instance;
                var operationId = CentralizedProgressService.GenerateOperationId("install");
                
                // Tính tổng số operations (devices * apks)
                var totalOperations = devices.Count * apks.Count;
                var completedOperations = 0;

                progressService.StartOperation(
                    operationId, 
                    "Installing APKs", 
                    $"Installing {apks.Count} APKs on {devices.Count} device(s)"
                );

                try
                {
                    foreach (var device in devices)
                    {
                        foreach (var apk in apks)
                        {
                            // Cập nhật progress trước khi cài đặt
                            var progress = (double)completedOperations / totalOperations * 100;
                            progressService.UpdateProgress(
                                operationId,
                                progress,
                                $"Installing {apk.FileName} on {device.DisplayName}",
                                totalOperations,
                                completedOperations
                            );

                            // Thực hiện cài đặt thực tế (gọi service gốc)
                            // await originalInstaller.InstallSingleApkAsync(device, apk, ...);
                            
                            // Log progress
                            logger($"Installing {apk.FileName} on {device.DisplayName}...");
                            
                            // Giả lập thời gian cài đặt
                            await Task.Delay(1000);
                            
                            completedOperations++;
                        }
                    }

                    progressService.CompleteOperation(
                        operationId, 
                        $"Successfully installed {apks.Count} APKs on {devices.Count} device(s)"
                    );
                }
                catch (Exception ex)
                {
                    logger($"Installation failed: {ex.Message}");
                    progressService.UpdateProgress(operationId, 0, $"Installation failed: {ex.Message}");
                    await Task.Delay(2000);
                    progressService.CancelOperation(operationId);
                    throw;
                }
            }
        }

        #endregion

        #region Cancellation Support Example

        /// <summary>
        /// Ví dụ: Hỗ trợ hủy operation với CancellationToken
        /// </summary>
        public static async Task CancellableOperationExample(CancellationToken cancellationToken)
        {
            var progressService = CentralizedProgressService.Instance;
            var operationId = CentralizedProgressService.GenerateOperationId("cancellable");
            
            progressService.StartOperation(operationId, "Long Running Task", "Processing items...");

            try
            {
                var totalItems = 10;
                
                for (int i = 0; i < totalItems; i++)
                {
                    // Kiểm tra cancellation
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var progress = (double)(i + 1) / totalItems * 100;
                    progressService.UpdateProgress(
                        operationId,
                        progress,
                        $"Processing item {i + 1}/{totalItems}",
                        totalItems,
                        i + 1
                    );

                    await Task.Delay(1000, cancellationToken);
                }

                progressService.CompleteOperation(operationId, "All items processed successfully");
            }
            catch (OperationCanceledException)
            {
                // Xử lý hủy operation
                progressService.CancelOperation(operationId);
                throw;
            }
            catch (Exception ex)
            {
                progressService.UpdateProgress(operationId, 0, $"Error: {ex.Message}");
                await Task.Delay(2000);
                progressService.CancelOperation(operationId);
                throw;
            }
        }

        #endregion
    }
}
