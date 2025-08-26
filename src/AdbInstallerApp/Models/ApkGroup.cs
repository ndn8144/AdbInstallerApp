using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace AdbInstallerApp.Models
{
    public class ApkGroup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public ObservableCollection<ApkItem> ApkItems { get; set; } = new();
        public string Color { get; set; } = "#667eea"; // Default color
        
        public int ApkCount => ApkItems?.Count ?? 0;
        public long TotalSize => ApkItems?.Sum(a => a.FileSize) ?? 0;
        public string DisplayName => string.IsNullOrEmpty(Name) ? "Unnamed Group" : Name;
        public string CreatedAtText => CreatedAt.ToString("yyyy-MM-dd HH:mm");
        public string GroupInfo => $"{ApkCount} APK(s) - {FormatFileSize(TotalSize)}";
        
        public ApkGroup()
        {
            ApkItems = new ObservableCollection<ApkItem>();
        }
        
        public ApkGroup(string name, string description = "") : this()
        {
            Name = name;
            Description = description;
        }
        
        public void AddApk(ApkItem apk)
        {
            if (apk != null && !ApkItems.Contains(apk))
            {
                ApkItems.Add(apk);
            }
        }
        
        public void RemoveApk(ApkItem apk)
        {
            if (apk != null)
            {
                ApkItems.Remove(apk);
            }
        }
        
        public void ClearApks()
        {
            ApkItems.Clear();
        }
        
        public bool ContainsApk(ApkItem apk)
        {
            return ApkItems.Contains(apk);
        }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Name) && Name.Length <= 100;
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public override bool Equals(object? obj)
        {
            if (obj is ApkGroup other)
            {
                return Id.Equals(other.Id);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
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