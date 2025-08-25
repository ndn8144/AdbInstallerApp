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

    }
}