using System.Windows.Controls;
using AdbInstallerApp.ViewModels;

namespace AdbInstallerApp.Views
{
    /// <summary>
    /// Interaction logic for InstalledAppsView.xaml
    /// </summary>
    public partial class InstalledAppsView : UserControl
    {
        public InstalledAppsView()
        {
            // InitializeComponent is not needed since we're embedding the UI directly in MainWindow
        }

        /// <summary>
        /// Sets the ViewModel for this view
        /// </summary>
        /// <param name="viewModel">The InstalledAppViewModel instance</param>
        public void SetViewModel(InstalledAppViewModel viewModel)
        {
            DataContext = viewModel;
        }
    }
}
