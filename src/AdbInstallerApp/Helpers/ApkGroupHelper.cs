using AdbInstallerApp.Models;
using AdbInstallerApp.ViewModels;
using System.Text.RegularExpressions;
using System.IO;

namespace AdbInstallerApp.Helpers
{
    public static class ApkGroupHelper
    {
        // Predefined color palette for groups
        public static readonly string[] GroupColors =
        {
            "#667eea", // Primary Blue
            "#764ba2", // Primary Purple  
            "#FF5722", // Orange
            "#4CAF50", // Green
            "#2196F3", // Blue
            "#9C27B0", // Purple
            "#FF9800", // Amber
            "#795548", // Brown
            "#607D8B", // Blue Grey
            "#E91E63", // Pink
            "#00BCD4", // Cyan
            "#8BC34A", // Light Green
            "#FFC107", // Yellow
            "#3F51B5", // Indigo
            "#009688"  // Teal
        };

        /// <summary>
        /// Validates group name according to business rules
        /// </summary>
        public static (bool IsValid, string ErrorMessage) ValidateGroupName(string name, IEnumerable<ApkGroupViewModel>? existingGroups = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return (false, "Group name cannot be empty");

            if (name.Length > 50)
                return (false, "Group name cannot exceed 50 characters");

            if (name.Length < 2)
                return (false, "Group name must be at least 2 characters");

            // Check for invalid characters
            if (Regex.IsMatch(name, @"[<>:""/\\|?*]"))
                return (false, "Group name contains invalid characters");

            // Check for reserved names
            var reservedNames = new[] { "con", "prn", "aux", "nul", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9" };
            if (reservedNames.Contains(name.ToLowerInvariant()))
                return (false, "Group name is reserved");

            // Check for duplicates
            if (existingGroups != null && existingGroups.Any(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return (false, "A group with this name already exists");

            return (true, string.Empty);
        }

        /// <summary>
        /// Validates group description
        /// </summary>
        public static (bool IsValid, string ErrorMessage) ValidateGroupDescription(string description)
        {
            if (description.Length > 200)
                return (false, "Description cannot exceed 200 characters");

            return (true, string.Empty);
        }

        /// <summary>
        /// Gets a random color for a new group
        /// </summary>
        public static string GetRandomGroupColor(IEnumerable<ApkGroupViewModel>? existingGroups = null)
        {
            var usedColors = existingGroups?.Select(g => g.Color).ToHashSet() ?? new HashSet<string>();
            var availableColors = GroupColors.Where(c => !usedColors.Contains(c)).ToArray();

            if (availableColors.Length == 0)
                availableColors = GroupColors; // Reuse colors if all are taken

            var random = new Random();
            return availableColors[random.Next(availableColors.Length)];
        }

        /// <summary>
        /// Suggests a group name based on APK files
        /// </summary>
        public static string SuggestGroupName(IEnumerable<ApkItemViewModel> apkItems)
        {
            if (!apkItems.Any())
                return "New Group";

            var apkList = apkItems.ToList();

            // Try to find common package name
            var packageNames = apkList
                .Where(a => !string.IsNullOrEmpty(a.Model.Package))
                .Select(a => ExtractAppName(a.Model.Package))
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();

            if (packageNames.Any())
            {
                var mostCommon = packageNames
                    .GroupBy(name => name)
                    .OrderByDescending(g => g.Count())
                    .First().Key;

                return CleanGroupName(mostCommon);
            }

            // Try to find common file name pattern
            var fileNames = apkList.Select(a => Path.GetFileNameWithoutExtension(a.FileName)).ToList();
            var commonPrefix = FindCommonPrefix(fileNames);

            if (!string.IsNullOrEmpty(commonPrefix) && commonPrefix.Length > 3)
            {
                return CleanGroupName(commonPrefix);
            }

            // Fallback to count-based name
            return $"Group with {apkList.Count} APK{(apkList.Count > 1 ? "s" : "")}";
        }

        /// <summary>
        /// Extracts app name from package name
        /// </summary>
        private static string ExtractAppName(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
                return string.Empty;

            // Remove common package prefixes
            var prefixes = new[] { "com.", "org.", "net.", "io.", "app." };
            var cleaned = packageName;

            foreach (var prefix in prefixes)
            {
                if (cleaned.StartsWith(prefix))
                {
                    cleaned = cleaned.Substring(prefix.Length);
                    break;
                }
            }

            // Take the first part (usually company/developer name)
            var parts = cleaned.Split('.');
            if (parts.Length > 0)
            {
                return parts[0];
            }

            return cleaned;
        }

        /// <summary>
        /// Finds common prefix among strings
        /// </summary>
        private static string FindCommonPrefix(IEnumerable<string> strings)
        {
            var stringList = strings.ToList();
            if (!stringList.Any())
                return string.Empty;

            var first = stringList.First();
            for (int i = 0; i < first.Length; i++)
            {
                if (stringList.Any(s => s.Length <= i || s[i] != first[i]))
                {
                    return first.Substring(0, i);
                }
            }

            return first;
        }

        /// <summary>
        /// Cleans and formats group name
        /// </summary>
        private static string CleanGroupName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "New Group";

            // Remove special characters and extra spaces
            name = Regex.Replace(name, @"[^a-zA-Z0-9\s\-_]", "");
            name = Regex.Replace(name, @"\s+", " ");
            name = name.Trim();

            // Capitalize first letter of each word
            name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());

            if (string.IsNullOrEmpty(name))
                return "New Group";

            return name.Length > 30 ? name.Substring(0, 30) + "..." : name;
        }

        /// <summary>
        /// Analyzes APK files and suggests optimal grouping
        /// </summary>
        public static List<SuggestedGroup> AnalyzeAndSuggestGroups(IEnumerable<ApkItemViewModel> apkItems)
        {
            var suggestions = new List<SuggestedGroup>();
            var apkList = apkItems.ToList();

            // Group by package name
            var packageGroups = apkList
                .Where(a => !string.IsNullOrEmpty(a.Model.Package))
                .GroupBy(a => a.Model.Package)
                .Where(g => g.Count() > 1) // Only suggest groups with multiple items
                .ToList();

            foreach (var group in packageGroups)
            {
                var appName = ExtractAppName(group.Key);
                suggestions.Add(new SuggestedGroup
                {
                    Name = $"{appName} Bundle",
                    Description = $"All APK files for {appName} application",
                    ApkItems = group.ToList(),
                    Reason = "Same package name",
                    Color = GetRandomGroupColor()
                });
            }

            // Group by file name patterns
            var nameGroups = apkList
                .GroupBy(a => ExtractAppName(Path.GetFileNameWithoutExtension(a.FileName)))
                .Where(g => g.Count() > 1 && !string.IsNullOrEmpty(g.Key))
                .Where(g => !suggestions.Any(s => s.ApkItems.Any(item => g.Contains(item))))
                .ToList();

            foreach (var group in nameGroups)
            {
                suggestions.Add(new SuggestedGroup
                {
                    Name = $"{group.Key} Collection",
                    Description = $"APK files with similar names: {group.Key}",
                    ApkItems = group.ToList(),
                    Reason = "Similar file names",
                    Color = GetRandomGroupColor()
                });
            }

            // Group by size (large apps)
            var largeApks = apkList
                .Where(a => a.Model.FileSize > 100 * 1024 * 1024) // > 100MB
                .Where(a => !suggestions.Any(s => s.ApkItems.Contains(a)))
                .ToList();

            if (largeApks.Count > 1)
            {
                suggestions.Add(new SuggestedGroup
                {
                    Name = "Large Apps",
                    Description = "APK files larger than 100MB",
                    ApkItems = largeApks,
                    Reason = "Large file size",
                    Color = "#FF9800" // Orange for large files
                });
            }

            return suggestions;
        }

        /// <summary>
        /// Gets group statistics
        /// </summary>
        public static GroupStatistics GetGroupStatistics(ApkGroupViewModel group)
        {
            var apks = group.ApkItems.ToList();

            return new GroupStatistics
            {
                TotalApks = apks.Count,
                TotalSize = apks.Sum(a => a.Model.FileSize),
                SelectedApks = apks.Count(a => a.IsSelected),
                SelectedSize = apks.Where(a => a.IsSelected).Sum(a => a.Model.FileSize),
                AverageSize = apks.Any() ? (long)apks.Average(a => a.Model.FileSize) : 0,
                CreatedDate = group.CreatedAt,
                LastModified = apks.Any() ? apks.Max(a => a.Model.LastModified) : DateTime.MinValue
            };
        }

        /// <summary>
        /// Exports group configuration to JSON
        /// </summary>
        public static string ExportGroupsToJson(IEnumerable<ApkGroupViewModel> groups)
        {
            var exportData = groups.Select(g => new
            {
                Name = g.Name,
                Description = g.Description,
                Color = g.Color,
                CreatedAt = g.CreatedAt,
                ApkFiles = g.ApkItems.Select(a => new
                {
                    FileName = a.FileName,
                    FilePath = a.FilePath,
                    Package = a.Model.Package,
                    FileSize = a.Model.FileSize
                }).ToList()
            }).ToList();

            return Newtonsoft.Json.JsonConvert.SerializeObject(exportData, Newtonsoft.Json.Formatting.Indented);
        }
    }

    /// <summary>
    /// Represents a suggested grouping for APK files
    /// </summary>
    public class SuggestedGroup
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<ApkItemViewModel> ApkItems { get; set; } = new();
        public string Reason { get; set; } = string.Empty;
        public string Color { get; set; } = "#667eea";
    }

    /// <summary>
    /// Statistics for a group
    /// </summary>
    public class GroupStatistics
    {
        public int TotalApks { get; set; }
        public long TotalSize { get; set; }
        public int SelectedApks { get; set; }
        public long SelectedSize { get; set; }
        public long AverageSize { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModified { get; set; }

        public string TotalSizeText => FormatFileSize(TotalSize);
        public string SelectedSizeText => FormatFileSize(SelectedSize);
        public string AverageSizeText => FormatFileSize(AverageSize);

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