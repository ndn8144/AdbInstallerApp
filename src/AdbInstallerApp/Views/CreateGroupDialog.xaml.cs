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
            
            // Set owner window trong constructor thay vì OnSourceInitialized
            SetOwnerWindow();
        }
        
        public CreateGroupDialog(CreateGroupDialogModel model) : this()
        {
            DialogModel = model;
            DataContext = DialogModel;
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
        
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
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