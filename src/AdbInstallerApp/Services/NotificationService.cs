using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;

namespace AdbInstallerApp.Services
{
    /// <summary>
    /// Service for handling notifications (sound, system tray, etc.)
    /// </summary>
    public class NotificationService : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private readonly SoundPlayer _soundPlayer;
        private bool _soundEnabled = true;
        private bool _systemTrayEnabled = true;
        
        public bool SoundEnabled 
        { 
            get => _soundEnabled; 
            set => _soundEnabled = value; 
        }
        
        public bool SystemTrayEnabled 
        { 
            get => _systemTrayEnabled; 
            set => _systemTrayEnabled = value; 
        }
        
        public NotificationService()
        {
            _soundPlayer = new SoundPlayer();
            InitializeSystemTray();
        }
        
        private void InitializeSystemTray()
        {
            try
            {
                _notifyIcon = new NotifyIcon
                {
                    Icon = SystemIcons.Application,
                    Text = "ADB Installer App",
                    Visible = false
                };
                
                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add("Show", null, (s, e) => ShowMainWindow());
                contextMenu.Items.Add("Hide", null, (s, e) => HideMainWindow());
                contextMenu.Items.Add("-");
                contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());
                
                _notifyIcon.ContextMenuStrip = contextMenu;
                _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
            }
            catch (Exception ex)
            {
                // System tray not available
                _systemTrayEnabled = false;
            }
        }
        
        /// <summary>
        /// Show completion notification with sound
        /// </summary>
        public void ShowCompletionNotification(string title, string message, NotificationType type = NotificationType.Success)
        {
            // Play sound
            if (_soundEnabled)
            {
                PlayNotificationSound(type);
            }
            
            // Show system tray notification
            if (_systemTrayEnabled && _notifyIcon != null)
            {
                ShowSystemTrayNotification(title, message, type);
            }
            
            // Show in-app notification (fallback)
            ShowInAppNotification(title, message, type);
        }
        
        /// <summary>
        /// Show progress notification for long operations
        /// </summary>
        public void ShowProgressNotification(string title, string message, double progress)
        {
            if (_systemTrayEnabled && _notifyIcon != null)
            {
                _notifyIcon.Text = $"{title} - {progress:F1}%";
                _notifyIcon.Visible = true;
                
                // Update balloon tip for significant progress milestones
                if (progress % 25 == 0 && progress > 0)
                {
                    _notifyIcon.ShowBalloonTip(2000, title, $"{message} - {progress:F1}% complete", ToolTipIcon.Info);
                }
            }
        }
        
        private void PlayNotificationSound(NotificationType type)
        {
            try
            {
                switch (type)
                {
                    case NotificationType.Success:
                        SystemSounds.Asterisk.Play();
                        break;
                    case NotificationType.Warning:
                        SystemSounds.Exclamation.Play();
                        break;
                    case NotificationType.Error:
                        SystemSounds.Hand.Play();
                        break;
                    case NotificationType.Info:
                        SystemSounds.Beep.Play();
                        break;
                }
            }
            catch
            {
                // Sound not available
            }
        }
        
        private void ShowSystemTrayNotification(string title, string message, NotificationType type)
        {
            if (_notifyIcon == null) return;
            
            var icon = type switch
            {
                NotificationType.Success => ToolTipIcon.Info,
                NotificationType.Warning => ToolTipIcon.Warning,
                NotificationType.Error => ToolTipIcon.Error,
                _ => ToolTipIcon.Info
            };
            
            _notifyIcon.Visible = true;
            _notifyIcon.ShowBalloonTip(5000, title, message, icon);
            
            // Auto-hide after notification
            Task.Delay(10000).ContinueWith(_ =>
            {
                if (_notifyIcon != null)
                    _notifyIcon.Visible = false;
            });
        }
        
        private void ShowInAppNotification(string title, string message, NotificationType type)
        {
            // Show in main window if available
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var messageBoxImage = type switch
                {
                    NotificationType.Success => MessageBoxImage.Information,
                    NotificationType.Warning => MessageBoxImage.Warning,
                    NotificationType.Error => MessageBoxImage.Error,
                    _ => MessageBoxImage.Information
                };
                
                // Only show for important notifications to avoid spam
                if (type == NotificationType.Success || type == NotificationType.Error)
                {
                    System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, messageBoxImage);
                }
            });
        }
        
        private void ShowMainWindow()
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.Show();
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                }
            });
        }
        
        private void HideMainWindow()
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var mainWindow = System.Windows.Application.Current.MainWindow;
                mainWindow?.Hide();
            });
        }
        
        private void ExitApplication()
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                System.Windows.Application.Current.Shutdown();
            });
        }
        
        public void Dispose()
        {
            _notifyIcon?.Dispose();
            _soundPlayer?.Dispose();
        }
    }
    
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }
}
