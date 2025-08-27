using System.Windows;
using System.Windows.Controls;
using AdbInstallerApp.Models;
using System;

namespace AdbInstallerApp.Views
{
    public partial class CreateGroupDialog : Window
    {
        public CreateGroupDialogModel DialogModel { get; set; }

        public CreateGroupDialog()
        {
            InitializeComponent();
            DialogModel = new CreateGroupDialogModel();
            DataContext = DialogModel;

            // Debug: Log để kiểm tra binding
            System.Diagnostics.Debug.WriteLine($"CreateGroupDialog: DialogModel initialized, GroupName = '{DialogModel.GroupName}'");
            System.Diagnostics.Debug.WriteLine($"CreateGroupDialog: DataContext set to DialogModel");

            // Set owner window trong constructor thay vì OnSourceInitialized
            SetOwnerWindow();
            
            // Debug: Kiểm tra binding sau khi khởi tạo
            this.Loaded += CreateGroupDialog_Loaded;
        }

        public CreateGroupDialog(CreateGroupDialogModel model) : this()
        {
            DialogModel = model;
            DataContext = DialogModel;
            System.Diagnostics.Debug.WriteLine($"CreateGroupDialog: Model passed, GroupName = '{DialogModel.GroupName}'");
        }

        private void CreateGroupDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // Debug: Kiểm tra binding khi dialog được load
            System.Diagnostics.Debug.WriteLine($"CreateGroupDialog: Dialog loaded, GroupName = '{DialogModel.GroupName}'");
            System.Diagnostics.Debug.WriteLine($"CreateGroupDialog: Create Group button IsEnabled = {CreateGroupButton?.IsEnabled}");
            
            // Force update binding
            if (CreateGroupButton != null)
            {
                CreateGroupButton.GetBindingExpression(Button.IsEnabledProperty)?.UpdateTarget();
            }
        }

        private void SetOwnerWindow()
        {
            try
            {
                // Set owner window nếu có thể
                if (Application.Current.MainWindow != null && Application.Current.MainWindow != this)
                {
                    this.Owner = Application.Current.MainWindow;
                }
            }
            catch (Exception ex)
            {
                // Log lỗi nếu có vấn đề với owner
                System.Diagnostics.Debug.WriteLine($"Error setting owner window: {ex.Message}");
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#667eea"));
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CCCCCC"));
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                System.Diagnostics.Debug.WriteLine($"TextBox_TextChanged: Text = '{textBox.Text}', GroupName = '{DialogModel.GroupName}'");
                
                // Force update button state
                if (CreateGroupButton != null)
                {
                    CreateGroupButton.GetBindingExpression(Button.IsEnabledProperty)?.UpdateTarget();
                    System.Diagnostics.Debug.WriteLine($"TextBox_TextChanged: Button IsEnabled = {CreateGroupButton.IsEnabled}");
                }
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"CreateGroupDialog: ConfirmButton_Click called, GroupName = '{DialogModel.GroupName}'");
            System.Diagnostics.Debug.WriteLine($"CreateGroupDialog: IsValid() = {DialogModel.IsValid()}");
            
            if (DialogModel.IsValid())
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please enter a valid group name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}