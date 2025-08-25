using AdbInstallerApp.Models;
using CommunityToolkit.Mvvm.ComponentModel; // cspell:disable-line
using System.ComponentModel;

namespace AdbInstallerApp.ViewModels
{
    public partial class ApkItemViewModel : ObservableObject
    {
        public ApkItem Model { get; }
        
        public ApkItemViewModel(ApkItem m) 
        { 
            Model = m ?? throw new ArgumentNullException(nameof(m)); 
            
            // Subscribe to property changes to notify parent ViewModel
            PropertyChanged += OnPropertyChanged;
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Remove this method to avoid potential circular references
            // The ObservableObject base class already handles property change notifications
        }

        public string FileName => Model?.FileName ?? "Unknown";
        
        public string FilePath => Model?.FilePath ?? "Unknown";
        
        public string FileSize 
        { 
            get 
            {
                if (Model.FileSize > 0)
                {
                    return FormatFileSize(Model.FileSize);
                }
                return "Unknown";
            }
        }
        
        public string LastModified 
        { 
            get 
            {
                if (Model?.LastModified != DateTime.MinValue)
                {
                    return Model!.LastModified.ToString("yyyy-MM-dd HH:mm");
                }
                return "Unknown";
            }
        }
        
        public string SplitTag => !string.IsNullOrEmpty(Model?.SplitTag) ? Model.SplitTag : "Base";
        
        public string DisplayInfo 
        { 
            get 
            {
                if (Model == null) return "Unknown APK";
                
                // Simple and direct return to avoid any binding issues
                if (!string.IsNullOrEmpty(Model.Package))
                {
                    return Model.Package.Trim();
                }
                
                if (!string.IsNullOrEmpty(Model.FileName))
                {
                    return Model.FileName.Trim();
                }
                
                return "Unknown APK";
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        
        public override string ToString()
        {
            return DisplayInfo;
        }
    }
}