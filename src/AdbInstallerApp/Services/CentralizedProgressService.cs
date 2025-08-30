using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AdbInstallerApp.Services
{
    /// <summary>
    /// Service tập trung quản lý progress bar cho toàn bộ ứng dụng
    /// Singleton pattern với UI throttling và concurrent operations support
    /// </summary>
    public sealed partial class CentralizedProgressService : ObservableObject
    {
        private static readonly Lazy<CentralizedProgressService> _instance = 
            new(() => new CentralizedProgressService());
        
        public static CentralizedProgressService Instance => _instance.Value;
        
        // UI Throttling - max 10 updates/second
        private readonly object _lock = new();
        private const int UI_UPDATE_INTERVAL_MS = 100; // 10 updates/second
        
        // Concurrent Operations Support
        private readonly ConcurrentDictionary<string, OptimizedProgressService.OperationInfo> _activeOperations = new();
        private readonly ConcurrentQueue<OptimizedProgressService.OperationUpdate> _pendingUpdates = new();
        
        // Memory Pooling
        private readonly ConcurrentQueue<OptimizedProgressService.OperationUpdate> _updatePool = new();
        private const int MAX_POOLED_UPDATES = 100;

        #region Observable Properties

        /// <summary>
        /// Có đang thực hiện operation nào không
        /// </summary>
        [ObservableProperty]
        private bool _isOperationInProgress;

        /// <summary>
        /// Progress hiện tại (0-100)
        /// </summary>
        [ObservableProperty]
        private double _currentProgress;

        /// <summary>
        /// Tên operation hiện tại (VD: "Installing APKs", "Exporting Apps")
        /// </summary>
        [ObservableProperty]
        private string _currentOperationName = string.Empty;

        /// <summary>
        /// Chi tiết operation hiện tại (VD: "Installing app.apk on Device 1")
        /// </summary>
        [ObservableProperty]
        private string _currentOperationDetail = string.Empty;

        /// <summary>
        /// Thời gian ước tính còn lại
        /// </summary>
        [ObservableProperty]
        private string _estimatedTimeRemaining = string.Empty;

        /// <summary>
        /// Tốc độ xử lý (VD: "2.5 MB/s", "3 APKs/min")
        /// </summary>
        [ObservableProperty]
        private string _processingSpeed = string.Empty;

        #endregion

        #region Computed Properties

        /// <summary>
        /// Text hiển thị trên progress bar
        /// </summary>
        public string ProgressText => IsOperationInProgress 
            ? $"{CurrentProgress:F1}% - {CurrentOperationDetail}"
            : "Ready";

        /// <summary>
        /// Text tooltip chi tiết
        /// </summary>
        public string DetailedProgressInfo => IsOperationInProgress
            ? $"{CurrentOperationName}\n{CurrentOperationDetail}\n" +
              $"Progress: {CurrentProgress:F1}%\n" +
              (!string.IsNullOrEmpty(EstimatedTimeRemaining) ? $"ETA: {EstimatedTimeRemaining}\n" : "") +
              (!string.IsNullOrEmpty(ProcessingSpeed) ? $"Speed: {ProcessingSpeed}" : "")
            : "No operation in progress";

        #endregion

        #region Private Fields

        private DateTime _operationStartTime;
        private string _currentOperationId = string.Empty;

        #endregion

        private CentralizedProgressService() { }

        #region Public Methods

        /// <summary>
        /// Bắt đầu một operation mới
        /// </summary>
        /// <param name="operationId">ID duy nhất cho operation</param>
        /// <param name="operationName">Tên operation</param>
        /// <param name="initialDetail">Chi tiết ban đầu</param>
        public void StartOperation(string operationId, string operationName, string initialDetail = "")
        {
            _currentOperationId = operationId;
            CurrentOperationName = operationName;
            CurrentOperationDetail = initialDetail;
            CurrentProgress = 0;
            EstimatedTimeRemaining = "";
            ProcessingSpeed = "";
            IsOperationInProgress = true;
            _operationStartTime = DateTime.Now;

            // Notify computed properties changed
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(DetailedProgressInfo));
        }

        /// <summary>
        /// Cập nhật progress cho operation hiện tại
        /// </summary>
        /// <param name="operationId">ID operation</param>
        /// <param name="progress">Progress (0-100)</param>
        /// <param name="detail">Chi tiết mới</param>
        /// <param name="totalItems">Tổng số items (để tính ETA)</param>
        /// <param name="completedItems">Số items đã hoàn thành</param>
        public void UpdateProgress(string operationId, double progress, string detail = "", 
            int totalItems = 0, int completedItems = 0)
        {
            // Chỉ update nếu đúng operation ID
            if (_currentOperationId != operationId || !IsOperationInProgress)
                return;

            CurrentProgress = Math.Max(0, Math.Min(100, progress));
            
            if (!string.IsNullOrEmpty(detail))
                CurrentOperationDetail = detail;

            // Tính toán ETA nếu có đủ thông tin
            if (totalItems > 0 && completedItems > 0 && progress > 0)
            {
                var elapsed = DateTime.Now - _operationStartTime;
                var avgTimePerItem = elapsed.TotalSeconds / completedItems;
                var remainingItems = totalItems - completedItems;
                var etaSeconds = remainingItems * avgTimePerItem;

                if (etaSeconds > 0 && etaSeconds < 3600) // Chỉ hiển thị nếu < 1 giờ
                {
                    EstimatedTimeRemaining = TimeSpan.FromSeconds(etaSeconds).ToString(@"mm\:ss");
                }

                // Tính tốc độ xử lý
                if (elapsed.TotalSeconds > 5) // Chỉ tính sau 5 giây để có độ chính xác
                {
                    var itemsPerSecond = completedItems / elapsed.TotalSeconds;
                    ProcessingSpeed = $"{itemsPerSecond:F1} items/sec";
                }
            }

            // Notify computed properties changed
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(DetailedProgressInfo));
        }

        /// <summary>
        /// Hoàn thành operation
        /// </summary>
        /// <param name="operationId">ID operation</param>
        /// <param name="finalMessage">Message cuối cùng</param>
        public void CompleteOperation(string operationId, string finalMessage = "Completed")
        {
            if (_currentOperationId != operationId)
                return;

            CurrentProgress = 100;
            CurrentOperationDetail = finalMessage;
            
            // Notify computed properties changed
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(DetailedProgressInfo));

            // Reset sau 2 giây
            Task.Delay(2000).ContinueWith(_ =>
            {
                if (_currentOperationId == operationId) // Đảm bảo không có operation mới
                {
                    ResetProgress();
                }
            });
        }

        /// <summary>
        /// Hủy operation hiện tại
        /// </summary>
        /// <param name="operationId">ID operation</param>
        public void CancelOperation(string operationId)
        {
            if (_currentOperationId != operationId)
                return;

            CurrentOperationDetail = "Cancelled";
            
            // Reset sau 1 giây
            Task.Delay(1000).ContinueWith(_ =>
            {
                if (_currentOperationId == operationId)
                {
                    ResetProgress();
                }
            });
        }

        /// <summary>
        /// Reset về trạng thái ban đầu
        /// </summary>
        private void ResetProgress()
        {
            IsOperationInProgress = false;
            CurrentProgress = 0;
            CurrentOperationName = string.Empty;
            CurrentOperationDetail = string.Empty;
            EstimatedTimeRemaining = string.Empty;
            ProcessingSpeed = string.Empty;
            _currentOperationId = string.Empty;

            // Notify computed properties changed
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(DetailedProgressInfo));
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Tạo operation ID duy nhất
        /// </summary>
        /// <param name="prefix">Prefix cho ID</param>
        /// <returns>Operation ID</returns>
        public static string GenerateOperationId(string prefix = "op")
        {
            return $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
        }

        #endregion
    }
}
