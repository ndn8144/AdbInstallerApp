using CommunityToolkit.Mvvm.ComponentModel;

namespace AdbInstallerApp.ViewModels
{
    public sealed partial class QueueItemVM : ObservableObject
    {
        [ObservableProperty]
        private string _groupId = string.Empty;

        [ObservableProperty]
        private string _packageName = string.Empty;

        [ObservableProperty]
        private string _detail = string.Empty;

        [ObservableProperty]
        private ApkGroupViewModel? _group;

        [ObservableProperty]
        private string _filePath = string.Empty;
    }
}
