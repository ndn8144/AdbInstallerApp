using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AdbInstallerApp.ViewModels;
using AdbInstallerApp.Models;


namespace AdbInstallerApp.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Closed += (_, __) => (DataContext as MainViewModel)?.PersistSettings();
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.LogText = string.Empty;
            }
        }

        // Auto-scroll to bottom when log text changes
        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is MainViewModel vm)
            {
                // Subscribe to property changed event for LogText
                vm.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(MainViewModel.LogText))
                    {
                        // Auto-scroll to bottom after a short delay to ensure text is rendered
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (LogScrollViewer != null)
                            {
                                LogScrollViewer.ScrollToBottom();
                            }
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                };
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var settingsDialog = new SettingsDialog(vm);
                settingsDialog.Owner = this;
                settingsDialog.ShowDialog();
            }
        }

        // Drag & Drop Event Handlers
        private void DataGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is DataGrid dataGrid)
            {
                var selectedItems = dataGrid.SelectedItems;
                if (selectedItems.Count > 0)
                {
                    var dataObject = new DataObject();
                    dataObject.SetData("APKItems", selectedItems);
                    DragDrop.DoDragDrop(dataGrid, dataObject, DragDropEffects.Copy);
                }
            }
        }

        private void DataGrid_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("APKItems"))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DataGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("APKItems") && sender is DataGrid dataGrid)
            {
                // Handle drop on DataGrid (could be used for reordering)
                if (DataContext is MainViewModel vm)
                {
                    vm.AppendLog("[INFO] Drag & Drop: APK items dropped on DataGrid");
                }
            }
        }

        private void TreeView_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("APKItems"))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void TreeView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("APKItems") && sender is TreeView treeView)
            {
                var droppedItems = e.Data.GetData("APKItems");
                if (DataContext is MainViewModel vm)
                {
                    // Get the target group from the drop position
                    var dropPoint = e.GetPosition(treeView);
                    var result = VisualTreeHelper.HitTest(treeView, dropPoint);
                    
                    if (result?.VisualHit != null)
                    {
                        // Find the parent ApkGroupViewModel
                        var parent = FindParent<FrameworkElement>(result.VisualHit);
                        if (parent?.DataContext is ApkGroupViewModel targetGroup)
                        {
                            // Add dropped APKs to the target group
                            vm.AppendLog($"[INFO] Adding APK items to group: {targetGroup.DisplayName}");
                            vm.AddDroppedApksToGroup(droppedItems, targetGroup);
                        }
                        else
                        {
                            vm.AppendLog("[INFO] Drag & Drop: Please drop on a specific APK group");
                        }
                    }
                }
            }
        }
        
        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            
            if (parentObject == null)
                return null;
                
            if (parentObject is T parent)
                return parent;
            else
                return FindParent<T>(parentObject);
        }
    }
}