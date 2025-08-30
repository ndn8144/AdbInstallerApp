using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AdbInstallerApp.Models;

namespace AdbInstallerApp.ViewModels
{
    /// <summary>
    /// ViewModel for device-specific installation options dialog
    /// </summary>
    public sealed class DeviceOptionsViewModel : INotifyPropertyChanged
    {
        private DeviceInstallOptions _options;
        private string _userIdText = "";
        private string _throttleText = "";
        private string _maxRetriesText = "2";
        private string _timeoutText = "300";
        private bool _shouldClose;
        private bool? _dialogResult;

        public event PropertyChangedEventHandler? PropertyChanged;

        public DeviceOptionsViewModel(string deviceSerial, string deviceModel, DeviceInstallOptions? currentOptions = null)
        {
            DeviceSerial = deviceSerial;
            DeviceModel = deviceModel;
            DeviceDisplayName = string.IsNullOrEmpty(deviceModel) ? deviceSerial : $"{deviceModel} ({deviceSerial})";
            
            _options = currentOptions ?? new DeviceInstallOptions();
            InitializeFromOptions();
            InitializeCommands();
        }

        #region Properties

        public string DeviceSerial { get; }
        public string DeviceModel { get; }
        public string DeviceDisplayName { get; }

        public DeviceInstallOptions Options
        {
            get => _options;
            set
            {
                if (_options != value)
                {
                    _options = value;
                    InitializeFromOptions();
                    OnPropertyChanged();
                }
            }
        }

        public string UserIdText
        {
            get => _userIdText;
            set
            {
                if (_userIdText != value)
                {
                    _userIdText = value;
                    UpdateOptionsFromText();
                    OnPropertyChanged();
                }
            }
        }

        public string ThrottleText
        {
            get => _throttleText;
            set
            {
                if (_throttleText != value)
                {
                    _throttleText = value;
                    UpdateOptionsFromText();
                    OnPropertyChanged();
                }
            }
        }

        public string MaxRetriesText
        {
            get => _maxRetriesText;
            set
            {
                if (_maxRetriesText != value)
                {
                    _maxRetriesText = value;
                    UpdateOptionsFromText();
                    OnPropertyChanged();
                }
            }
        }

        public string TimeoutText
        {
            get => _timeoutText;
            set
            {
                if (_timeoutText != value)
                {
                    _timeoutText = value;
                    UpdateOptionsFromText();
                    OnPropertyChanged();
                }
            }
        }

        public bool ShouldClose
        {
            get => _shouldClose;
            private set
            {
                if (_shouldClose != value)
                {
                    _shouldClose = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool? DialogResult
        {
            get => _dialogResult;
            private set
            {
                if (_dialogResult != value)
                {
                    _dialogResult = value;
                    OnPropertyChanged();
                }
            }
        }

        public IEnumerable<SplitMatchModeItem> AvailableSplitModes { get; } = new[]
        {
            new SplitMatchModeItem(StrictSplitMatchMode.Strict, "Strict", 
                "Require all mandatory splits (ABI/DPI/requiredSplit/feature)"),
            new SplitMatchModeItem(StrictSplitMatchMode.Relaxed, "Relaxed", 
                "Skip non-matching splits (ABI/DPI/locale)"),
            new SplitMatchModeItem(StrictSplitMatchMode.BaseOnlyFallback, "Base-Only Fallback", 
                "Fall back to base-only installation if splits don't match")
        };

        public IEnumerable<InstallStrategyItem> AvailableInstallStrategies { get; } = new[]
        {
            new InstallStrategyItem(InstallStrategy.Auto, "Auto", 
                "Auto-select based on file count and path complexity (recommended)"),
            new InstallStrategyItem(InstallStrategy.InstallMultiple, "Install Multiple", 
                "Use adb install-multiple command (faster for simple cases)"),
            new InstallStrategyItem(InstallStrategy.PmSession, "PM Session", 
                "Use pm install-create/write/commit (better for complex cases)")
        };

        #endregion

        #region Commands

        public ICommand ApplyPresetCommand { get; private set; } = null!;
        public ICommand ResetToGlobalCommand { get; private set; } = null!;
        public ICommand ApplyCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            ApplyPresetCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<string>(ApplyPreset);
            ResetToGlobalCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ResetToGlobal);
            ApplyCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(Apply);
        }

        private void ApplyPreset(string? presetName)
        {
            switch (presetName)
            {
                case "Strict":
                    Options = new DeviceInstallOptions(
                        Reinstall: false,
                        AllowDowngrade: false,
                        GrantRuntimePermissions: false,
                        UserId: null,
                        PreferHighestCompatible: true,
                        StrictSplitMatch: StrictSplitMatchMode.Strict,
                        InstallStrategy: InstallStrategy.Auto,
                        VerifySignature: true,
                        VerifyVersionHomogeneity: true,
                        ThrottleMBps: null,
                        MaxRetries: 2,
                        Timeout: TimeSpan.FromMinutes(5)
                    );
                    break;

                case "Relaxed":
                    Options = new DeviceInstallOptions(
                        Reinstall: true,
                        AllowDowngrade: true,
                        GrantRuntimePermissions: true,
                        UserId: null,
                        PreferHighestCompatible: true,
                        StrictSplitMatch: StrictSplitMatchMode.Relaxed,
                        InstallStrategy: InstallStrategy.Auto,
                        VerifySignature: false,
                        VerifyVersionHomogeneity: false,
                        ThrottleMBps: null,
                        MaxRetries: 3,
                        Timeout: TimeSpan.FromMinutes(10)
                    );
                    break;

                case "BaseOnly":
                    Options = new DeviceInstallOptions(
                        Reinstall: true,
                        AllowDowngrade: false,
                        GrantRuntimePermissions: false,
                        UserId: null,
                        PreferHighestCompatible: true,
                        StrictSplitMatch: StrictSplitMatchMode.BaseOnlyFallback,
                        InstallStrategy: InstallStrategy.InstallMultiple,
                        VerifySignature: true,
                        VerifyVersionHomogeneity: true,
                        ThrottleMBps: null,
                        MaxRetries: 2,
                        Timeout: TimeSpan.FromMinutes(5)
                    );
                    break;
            }
        }

        private void ResetToGlobal()
        {
            Options = new DeviceInstallOptions();
        }

        private void Apply()
        {
            DialogResult = true;
            ShouldClose = true;
        }

        #endregion

        #region Private Methods

        private void InitializeFromOptions()
        {
            _userIdText = _options.UserId?.ToString() ?? "";
            _throttleText = _options.ThrottleMBps?.ToString() ?? "";
            _maxRetriesText = _options.MaxRetries.ToString();
            _timeoutText = ((int)_options.EffectiveTimeout.TotalSeconds).ToString();
            
            // Notify all properties changed
            OnPropertyChanged(nameof(UserIdText));
            OnPropertyChanged(nameof(ThrottleText));
            OnPropertyChanged(nameof(MaxRetriesText));
            OnPropertyChanged(nameof(TimeoutText));
        }

        private void UpdateOptionsFromText()
        {
            int? userId = null;
            if (int.TryParse(_userIdText, out var userIdValue) && userIdValue >= 0)
                userId = userIdValue;

            int? throttle = null;
            if (int.TryParse(_throttleText, out var throttleValue) && throttleValue > 0)
                throttle = throttleValue;

            int maxRetries = 2;
            if (int.TryParse(_maxRetriesText, out var retriesValue) && retriesValue >= 0)
                maxRetries = retriesValue;

            var timeout = TimeSpan.FromMinutes(5);
            if (int.TryParse(_timeoutText, out var timeoutValue) && timeoutValue > 0)
                timeout = TimeSpan.FromSeconds(timeoutValue);

            _options = _options with
            {
                UserId = userId,
                ThrottleMBps = throttle,
                MaxRetries = maxRetries,
                Timeout = timeout
            };
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Display item for split match modes
    /// </summary>
    public record SplitMatchModeItem(StrictSplitMatchMode Mode, string DisplayName, string Description);

    /// <summary>
    /// Display item for install strategies
    /// </summary>
    public record InstallStrategyItem(InstallStrategy Strategy, string DisplayName, string Description);

    /// <summary>
    /// Simple relay command implementation
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();
    }

    /// <summary>
    /// Generic relay command implementation
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

        public void Execute(object? parameter) => _execute((T?)parameter);
    }
}
