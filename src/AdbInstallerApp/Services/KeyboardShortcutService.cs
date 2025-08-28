using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows;

namespace AdbInstallerApp.Services
{
    /// <summary>
    /// Service for handling global keyboard shortcuts
    /// </summary>
    public class KeyboardShortcutService
    {
        private readonly Dictionary<KeyGesture, Action> _shortcuts = new();
        private readonly EnhancedInstallQueue _installQueue;
        private readonly OptimizedProgressService _progressService;
        
        public KeyboardShortcutService(EnhancedInstallQueue installQueue, OptimizedProgressService progressService)
        {
            _installQueue = installQueue;
            _progressService = progressService;
            RegisterDefaultShortcuts();
        }
        
        private void RegisterDefaultShortcuts()
        {
            // Ctrl+C - Cancel current operation
            RegisterShortcut(new KeyGesture(Key.C, ModifierKeys.Control), () =>
            {
                _installQueue.CancelAll();
            });
            
            // Ctrl+P - Pause/Resume operations
            RegisterShortcut(new KeyGesture(Key.P, ModifierKeys.Control), () =>
            {
                if (_installQueue.IsPaused)
                    _installQueue.ResumeAll();
                else
                    _installQueue.PauseAll();
            });
            
            // Ctrl+Shift+C - Clear operation history
            RegisterShortcut(new KeyGesture(Key.C, ModifierKeys.Control | ModifierKeys.Shift), () =>
            {
                _progressService.ClearHistory();
            });
            
            // F5 - Refresh/Restart current operation
            RegisterShortcut(new KeyGesture(Key.F5), () =>
            {
                // TODO: Implement restart functionality
            });
            
            // Escape - Cancel current operation (alternative to Ctrl+C)
            RegisterShortcut(new KeyGesture(Key.Escape), () =>
            {
                var activeOps = _progressService.GetActiveOperations();
                foreach (var op in activeOps)
                {
                    _installQueue.CancelOperation(op.Id);
                }
            });
        }
        
        public void RegisterShortcut(KeyGesture gesture, Action action)
        {
            _shortcuts[gesture] = action;
        }
        
        public void UnregisterShortcut(KeyGesture gesture)
        {
            _shortcuts.Remove(gesture);
        }
        
        public bool HandleKeyDown(KeyEventArgs e)
        {
            var gesture = new KeyGesture(e.Key, Keyboard.Modifiers);
            
            if (_shortcuts.TryGetValue(gesture, out var action))
            {
                try
                {
                    action.Invoke();
                    e.Handled = true;
                    return true;
                }
                catch (Exception ex)
                {
                    // Log error but don't crash
                    System.Diagnostics.Debug.WriteLine($"Keyboard shortcut error: {ex.Message}");
                }
            }
            
            return false;
        }
        
        public IEnumerable<(KeyGesture Gesture, string Description)> GetRegisteredShortcuts()
        {
            yield return (new KeyGesture(Key.C, ModifierKeys.Control), "Cancel all operations");
            yield return (new KeyGesture(Key.P, ModifierKeys.Control), "Pause/Resume operations");
            yield return (new KeyGesture(Key.C, ModifierKeys.Control | ModifierKeys.Shift), "Clear operation history");
            yield return (new KeyGesture(Key.F5), "Refresh/Restart operation");
            yield return (new KeyGesture(Key.Escape), "Cancel current operation");
        }
    }
}
