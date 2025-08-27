using System.Windows;
using System.Windows.Controls;
using AdbInstallerApp.ViewModels;
using System;
using System.Windows.Input;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using AdbInstallerApp.Models;

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

            // Handle keyboard shortcuts
            this.KeyDown += MainWindow_KeyDown;
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not MainViewModel viewModel) return;

            // Ctrl+G: Toggle Groups View
            if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.Control)
            {
                viewModel.ToggleGroupsViewCommand.Execute(null);
                e.Handled = true;
            }
            // Ctrl+A: Select All APKs
            else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                viewModel.SelectAllApksCommand.Execute(null);
                e.Handled = true;
            }
            // Ctrl+D: Clear Selection
            else if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Control)
            {
                viewModel.ClearApkSelectionCommand.Execute(null);
                e.Handled = true;
            }
            // F5: Refresh
            else if (e.Key == Key.F5)
            {
                viewModel.RefreshAllCommand.Execute(null);
                e.Handled = true;
            }
            // Ctrl+N: Create New Group (when in groups view)
            else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control && viewModel.ShowGroupsView)
            {
                FocusNewGroupNameTextBox();
                e.Handled = true;
            }
        }

        private void FocusNewGroupNameTextBox()
        {
            // Find and focus the new group name textbox
            var textBox = FindVisualChild<TextBox>(this, "NewGroupNameTextBox");
            if (textBox != null)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent, string name = "") where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                
                if (child is T childOfType)
                {
                    if (string.IsNullOrEmpty(name) || (child as FrameworkElement)?.Name == name)
                        return childOfType;
                }

                var childOfChild = FindVisualChild<T>(child, name);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
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

        // Log-related methods that are referenced in XAML
        private void LogScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Auto-scroll to bottom if enabled
            if (sender is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToBottom();
            }
        }

        private void GoToBottom_Click(object sender, RoutedEventArgs e)
        {
            // Find the log scroll viewer and scroll to bottom
            var logScrollViewer = FindVisualChild<ScrollViewer>(this, "LogScrollViewer");
            logScrollViewer?.ScrollToBottom();
        }

        private void AddLogEntry(string message)
        {
            // This method can be used to add log entries programmatically
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.LogText += $"\n[{DateTime.Now:HH:mm:ss}] {message}";
            }
        }

        // APK Groups specific event handlers
        private void GroupExpander_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ApkGroupViewModel group)
            {
                group.IsExpanded = !group.IsExpanded;
            }
        }

        private void GroupCard_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ApkGroupViewModel group)
            {
                // Handle double click by checking click count
                if (e.ClickCount == 2)
                {
                    group.IsExpanded = !group.IsExpanded;
                    e.Handled = true;
                }
            }
        }

        private void ApkItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ApkItemViewModel apkItem)
            {
                // Handle double click by checking click count
                if (e.ClickCount == 2)
                {
                    // Toggle selection on double click
                    apkItem.IsSelected = !apkItem.IsSelected;
                    e.Handled = true;
                }
            }
        }

        // Drag and Drop support (future enhancement)
        private void GroupCard_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (sender is Border border && 
                    border.DataContext is ApkGroupViewModel group &&
                    DataContext is MainViewModel viewModel)
                {
                    if (e.Data.GetDataPresent(typeof(ApkItemViewModel[])))
                    {
                        var apkItems = e.Data.GetData(typeof(ApkItemViewModel[])) as ApkItemViewModel[];
                        if (apkItems != null)
                        {
                            foreach (var apk in apkItems)
                            {
                                if (!group.Model.ContainsApk(apk.Model))
                                {
                                    group.AddApk(apk);
                                }
                            }
                            
                            // Log the action
                            viewModel.LogText += $"\n[{DateTime.Now:HH:mm:ss}] ➕ Added {apkItems.Length} APK(s) to group '{group.Name}' via drag & drop";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in drag & drop: {ex.Message}");
            }
        }

        private void GroupCard_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(ApkItemViewModel[])))
            {
                e.Effects = DragDropEffects.Copy;
                
                // Visual feedback
                if (sender is Border border)
                {
                    border.Opacity = 0.8;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void GroupCard_DragLeave(object sender, DragEventArgs e)
        {
            // Reset visual feedback
            if (sender is Border border)
            {
                border.Opacity = 1.0;
            }
        }

        private void ApkItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ApkItemViewModel apkItem)
            {
                // Start drag operation if mouse is held down
                if (e.ClickCount == 1)
                {
                    // Store the start point for potential drag operation
                    border.Tag = e.GetPosition(border);
                }
            }
        }

        private void ApkItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed &&
                sender is Border border &&
                border.DataContext is ApkItemViewModel apkItem &&
                border.Tag is Point startPoint)
            {
                var currentPoint = e.GetPosition(border);
                var diff = startPoint - currentPoint;

                // Check if we've moved far enough to start a drag
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // Get all selected APKs for multi-select drag
                    var selectedApks = GetSelectedApkItems().ToArray();
                    if (selectedApks.Length == 0)
                    {
                        selectedApks = new[] { apkItem };
                    }

                    var data = new DataObject(typeof(ApkItemViewModel[]), selectedApks);
                    System.Windows.DragDrop.DoDragDrop(border, data, DragDropEffects.Copy);
                    
                    // Clear the start point
                    border.Tag = null;
                }
            }
        }

        private IEnumerable<ApkItemViewModel> GetSelectedApkItems()
        {
            if (DataContext is MainViewModel viewModel)
            {
                // Get selected from main list
                foreach (var apk in viewModel.ApkFiles.Where(a => a.IsSelected))
                {
                    yield return apk;
                }

                // Get selected from groups
                foreach (var group in viewModel.ApkGroups)
                {
                    foreach (var apk in group.ApkItems.Where(a => a.IsSelected))
                    {
                        yield return apk;
                    }
                }
            }
        }

        // Context menu helpers
        private void ShowApkContextMenu(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right && 
                sender is FrameworkElement element && 
                element.DataContext is ApkItemViewModel apkItem &&
                DataContext is MainViewModel viewModel)
            {
                var contextMenu = new ContextMenu();
                
                // Add to group menu items
                if (viewModel.ApkGroups.Any())
                {
                    var addToGroupMenuItem = new MenuItem { Header = "Add to Group" };
                    
                    foreach (var group in viewModel.ApkGroups)
                    {
                        var groupMenuItem = new MenuItem 
                        { 
                            Header = group.DisplayName,
                            Command = viewModel.AddSelectedApksToGroupCommand,
                            CommandParameter = group
                        };
                        addToGroupMenuItem.Items.Add(groupMenuItem);
                    }
                    
                    contextMenu.Items.Add(addToGroupMenuItem);
                }
                else
                {
                    contextMenu.Items.Add(new MenuItem 
                    { 
                        Header = "No groups available",
                        IsEnabled = false
                    });
                }
                
                // Show APK info
                contextMenu.Items.Add(new Separator());
                contextMenu.Items.Add(new MenuItem 
                { 
                    Header = $"APK: {apkItem.DisplayInfo}",
                    IsEnabled = false
                });
                contextMenu.Items.Add(new MenuItem 
                { 
                    Header = $"Size: {apkItem.FileSize}",
                    IsEnabled = false
                });
                contextMenu.Items.Add(new MenuItem 
                { 
                    Header = $"Modified: {apkItem.LastModified}",
                    IsEnabled = false
                });

                element.ContextMenu = contextMenu;
                contextMenu.IsOpen = true;
            }
        }

        // Quick filter/search functionality (bonus feature)
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox searchBox && DataContext is MainViewModel viewModel)
            {
                var searchText = searchBox.Text?.ToLowerInvariant() ?? string.Empty;
                
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    // Show all items
                    foreach (var apk in viewModel.ApkFiles)
                    {
                        SetItemVisibility(apk, true);
                    }
                    
                    foreach (var group in viewModel.ApkGroups)
                    {
                        SetGroupVisibility(group, true);
                    }
                }
                else
                {
                    // Filter items
                    foreach (var apk in viewModel.ApkFiles)
                    {
                        var visible = apk.DisplayInfo.ToLowerInvariant().Contains(searchText) ||
                                     apk.FileName.ToLowerInvariant().Contains(searchText);
                        SetItemVisibility(apk, visible);
                    }
                    
                    // Filter groups and their items
                    foreach (var group in viewModel.ApkGroups)
                    {
                        var groupVisible = group.DisplayName.ToLowerInvariant().Contains(searchText) ||
                                         group.Description.ToLowerInvariant().Contains(searchText);
                        
                        var hasVisibleItems = false;
                        foreach (var apk in group.ApkItems)
                        {
                            var itemVisible = apk.DisplayInfo.ToLowerInvariant().Contains(searchText) ||
                                            apk.FileName.ToLowerInvariant().Contains(searchText);
                            if (itemVisible) hasVisibleItems = true;
                        }
                        
                        SetGroupVisibility(group, groupVisible || hasVisibleItems);
                    }
                }
            }
        }

        private void SetItemVisibility(ApkItemViewModel item, bool visible)
        {
            // This would require additional property binding in the ViewModel
            // For now, we'll just mark it as a placeholder for future implementation
            // item.IsVisible = visible;
        }

        private void SetGroupVisibility(ApkGroupViewModel group, bool visible)
        {
            // This would require additional property binding in the ViewModel
            // For now, we'll just mark it as a placeholder for future implementation
            // group.IsVisible = visible;
        }

        private void OpenCreateGroupDialog()
        {
            try
            {
                var dialog = new CreateGroupDialog();
                var result = dialog.ShowDialog();
                
                if (result == true && dialog.DataContext is CreateGroupDialogModel dialogModel)
                {
                    // Gọi command từ ViewModel thay vì xử lý trực tiếp
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.NewGroupName = dialogModel.GroupName.Trim();
                        viewModel.NewGroupDescription = dialogModel.Description.Trim();
                        viewModel.CreateGroupCommand.Execute(null);
                    }
                }
            }
            catch (Exception ex)
            {
                // Thay vì gọi AppendLog, sử dụng MessageBox trực tiếp
                MessageBox.Show($"Error opening dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }  
        private void NewGroupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Debug: Log trạng thái hiện tại
                if (DataContext is MainViewModel mainViewModel)
                {
                    System.Diagnostics.Debug.WriteLine($"Debug: ShowGroupsView = {mainViewModel.ShowGroupsView}");
                    System.Diagnostics.Debug.WriteLine($"Debug: ApkGroups.Count = {mainViewModel.ApkGroups.Count}");
                    System.Diagnostics.Debug.WriteLine($"Debug: CurrentModule = {mainViewModel.CurrentModule}");
                }
                
                var dialog = new CreateGroupDialog();
                var result = dialog.ShowDialog();
                
                if (result == true && dialog.DataContext is CreateGroupDialogModel dialogModel)
                {
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.NewGroupName = dialogModel.GroupName.Trim();
                        viewModel.NewGroupDescription = dialogModel.Description.Trim();
                        viewModel.CreateGroupCommand.Execute(null);
                        
                        // Debug: Log sau khi tạo group
                        System.Diagnostics.Debug.WriteLine($"Debug: After creating group - ApkGroups.Count = {viewModel.ApkGroups.Count}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveApkFromGroup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is ApkItemViewModel apkItem)
                {
                    // Tìm group chứa APK này
                    if (DataContext is MainViewModel viewModel)
                    {
                        foreach (var group in viewModel.ApkGroups)
                        {
                            if (group.ApkItems.Contains(apkItem))
                            {
                                // Gọi command để remove APK khỏi group
                                var parameters = new object[] { group, apkItem };
                                viewModel.RemoveApkFromGroupCommand.Execute(parameters);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing APK from group: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}