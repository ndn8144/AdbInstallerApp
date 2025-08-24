using System.Windows;
using AdbInstallerApp.ViewModels;

namespace AdbInstallerApp.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Closed += (_, __) => (DataContext as MainViewModel)?.PersistSettings();
        }
    }
}