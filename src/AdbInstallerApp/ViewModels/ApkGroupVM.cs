using AdbInstallerApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdbInstallerApp.ViewModels
{
    public sealed partial class ApkGroupVM : ObservableObject
    {
        public ApkGroup Model { get; }

        public ApkGroupVM(ApkGroup model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }

        // Basic properties
        public string Id => Model.Id;
        public string Name => Model.Name;
        public string Description => Model.Description ?? "";
        public int ApkCount => Model.ApkCount;
        public string Subtitle => $"{ApkCount} APK{(ApkCount != 1 ? "s" : "")} • {FormatFileSize(Model.TotalSize)}";

        public bool Matches(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            
            var lowerFilter = filter.ToLowerInvariant();
            return Name.ToLowerInvariant().Contains(lowerFilter) ||
                   Description.ToLowerInvariant().Contains(lowerFilter);
        }

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
    }
}
