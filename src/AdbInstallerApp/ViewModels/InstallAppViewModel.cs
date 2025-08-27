using AdbInstallerApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace AdbInstallerApp.ViewModels
{
    public partial class InstalledAppViewModel : ObservableObject
    {
        public InstalledApp Model { get; }
        
        public InstalledAppViewModel(InstalledApp model) 
        { 
            Model = model ?? throw new ArgumentNullException(nameof(model)); 
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string PackageName => Model?.PackageName ?? "Unknown";
        
        public string DisplayName => Model?.DisplayName ?? "Unknown App";
        
        public string VersionInfo => Model?.VersionInfo ?? "Unknown";
        
        public string AppType => Model?.AppType ?? "Unknown";
        
        public string SizeInfo 
        { 
            get 
            {
                if (Model?.TotalSizeBytes is long size && size > 0)
                {
                    return FormatFileSize(size);
                }
                return "Size unknown";
            }
        }
        
        public string SplitInfo => Model?.SplitInfo ?? "Unknown";
        
        public string CodePathsText 
        { 
            get 
            {
                if (Model?.CodePaths != null && Model.CodePaths.Count > 0)
                {
                    var firstPath = Model.CodePaths[0];
                    if (Model.CodePaths.Count == 1)
                    {
                        return firstPath;
                    }
                    else
                    {
                        return $"{firstPath} (+{Model.CodePaths.Count - 1} more)";
                    }
                }
                return "Unknown";
            }
        }

        public bool HasSplits => Model?.HasSplits == true;

        public bool IsSystemApp => Model?.IsSystemApp == true;

        public bool IsUserApp => !IsSystemApp;

        private static string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
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
            return DisplayName;
        }
    }
}