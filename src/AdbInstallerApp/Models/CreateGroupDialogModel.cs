using CommunityToolkit.Mvvm.ComponentModel;

namespace AdbInstallerApp.Models
{
    public partial class CreateGroupDialogModel : ObservableObject
    {
        [ObservableProperty]
        private string _groupName = string.Empty;
        
        [ObservableProperty]
        private string _description = string.Empty;
        
        public bool IsConfirmed { get; set; } = false;
        
        public void Reset()
        {
            GroupName = string.Empty;
            Description = string.Empty;
            IsConfirmed = false;
        }
        
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(GroupName.Trim()) && GroupName.Trim().Length <= 100;
        }
    }
}