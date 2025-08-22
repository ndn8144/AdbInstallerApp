using System.Windows;
using AdbInstallerApp.ViewModels;

namespace AdbInstallerApp.Views
{
    public partial class SettingsDialog : Window
    {
        public SettingsDialog(MainViewModel mainViewModel)
        {
            InitializeComponent();
            DataContext = mainViewModel;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.PersistSettings();
                DialogResult = true;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
