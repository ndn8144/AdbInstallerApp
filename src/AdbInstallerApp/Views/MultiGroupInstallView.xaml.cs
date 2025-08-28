using AdbInstallerApp.ViewModels;
using System.Windows.Controls;

namespace AdbInstallerApp.Views
{
    /// <summary>
    /// Interaction logic for MultiGroupInstallView.xaml
    /// </summary>
    public partial class MultiGroupInstallView : UserControl
    {
        public MultiGroupInstallView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Set ViewModel cho view này
        /// </summary>
        public void SetViewModel(MultiGroupInstallViewModel viewModel)
        {
            DataContext = viewModel;
        }
    }
}
