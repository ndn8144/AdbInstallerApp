using System.Windows;
using System.Windows.Controls;
using AdbInstallerApp.ViewModels;
using System;

namespace AdbInstallerApp.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Closed += (_, __) => 
            {
                try
                {
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.PersistSettings();
                        viewModel.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error closing MainWindow: {ex.Message}");
                }
            };
        }

        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            // Cancel auto-generation completely to prevent duplicate columns
            e.Cancel = true;
        }

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            try
            {
                if (e.Row.Item == null)
                {
                    e.Row.Visibility = System.Windows.Visibility.Collapsed;
                }
                else
                {
                    e.Row.Visibility = System.Windows.Visibility.Visible;
                }
            }
            catch
            {
                e.Row.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void ApkFilesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // This event handler ensures that when DataGrid selection changes,
                // the ViewModel properties are properly updated
                if (DataContext is MainViewModel viewModel)
                {
                    // Use Dispatcher to ensure UI thread safety
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            // Force refresh of computed properties by accessing them
                            // This will trigger the property change notifications
                            _ = viewModel.SelectedApksCount;
                            _ = viewModel.SelectedApksCountText;
                        }
                        catch (Exception ex)
                        {
                            // Log error but don't crash
                            System.Diagnostics.Debug.WriteLine($"Error in SelectionChanged handler: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                System.Diagnostics.Debug.WriteLine($"Error in ApkFilesDataGrid_SelectionChanged: {ex.Message}");
            }
        }

        private void LogScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Auto-scroll to bottom if auto-scroll is enabled and user is near bottom
            if (AutoScrollCheckBox.IsChecked == true)
            {
                if (e.ExtentHeightChange > 0 || e.ViewportHeightChange > 0)
                {
                    LogScrollViewer.ScrollToBottom();
                }
            }
        }

        private void GoToBottom_Click(object sender, RoutedEventArgs e)
        {
            LogScrollViewer.ScrollToBottom();
            AutoScrollCheckBox.IsChecked = true; // Enable auto-scroll when manually going to bottom
        }

        // Method to add log entry with auto-scroll support
        public void AddLogEntry(string message)
        {
            // Add timestamp
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}\n";
            
            // Update the bound property
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.LogText += logEntry;
            }
            
            // Auto-scroll if enabled
            if (AutoScrollCheckBox.IsChecked == true)
            {
                LogScrollViewer.ScrollToBottom();
            }
        }
    }
}