using AdbInstallerApp.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace AdbInstallerApp.Services
{
    public class ApkRepoIndexer : IDisposable
    {
        private FileSystemWatcher? _watcher;
        public ObservableCollection<ApkItem> Items { get; } = new();
        public string RepoPath { get; private set; } = string.Empty;

        public void SetRepo(string? path)
        {
            RepoPath = path ?? string.Empty;
            Refresh();
            SetupWatcher();
        }

        private void SetupWatcher()
        {
            _watcher?.Dispose();
            if (string.IsNullOrWhiteSpace(RepoPath) || !Directory.Exists(RepoPath)) return;
            _watcher = new FileSystemWatcher(RepoPath, "*.apk")
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            _watcher.Created += (_, __) => Refresh();
            _watcher.Deleted += (_, __) => Refresh();
            _watcher.Renamed += (_, __) => Refresh();
            _watcher.Changed += (_, __) => Refresh();
        }

        public void Refresh()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Items.Clear();
                if (!string.IsNullOrWhiteSpace(RepoPath) && Directory.Exists(RepoPath))
                {
                    var apkFiles = Directory.GetFiles(RepoPath, "*.apk");
                    var apkGroups = GroupApkFiles(apkFiles);

                    foreach (var group in apkGroups)
                    {
                        foreach (var apkFile in group)
                        {
                            var apkItem = AnalyzeApkFile(apkFile);
                            if (apkItem != null)
                            {
                                Items.Add(apkItem);
                            }
                        }
                    }
                }
            });
        }

        private List<List<string>> GroupApkFiles(string[] apkFiles)
        {
            var groups = new List<List<string>>();
            var processedFiles = new HashSet<string>();

            foreach (var apkFile in apkFiles)
            {
                if (processedFiles.Contains(apkFile)) continue;

                var fileName = Path.GetFileNameWithoutExtension(apkFile);
                var group = new List<string> { apkFile };
                processedFiles.Add(apkFile);

                // Look for related split APKs
                foreach (var otherFile in apkFiles)
                {
                    if (otherFile == apkFile || processedFiles.Contains(otherFile)) continue;

                    var otherFileName = Path.GetFileNameWithoutExtension(otherFile);
                    if (IsRelatedSplitApk(fileName, otherFileName))
                    {
                        group.Add(otherFile);
                        processedFiles.Add(otherFile);
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        private bool IsRelatedSplitApk(string baseName, string otherName)
        {
            if (baseName == otherName) return false;

            // Convert to lowercase for case-insensitive comparison
            var lowerBaseName = baseName.ToLowerInvariant();
            var lowerOtherName = otherName.ToLowerInvariant();

            // Check if one is a prefix of the other
            if (lowerOtherName.StartsWith(lowerBaseName + "-") || lowerBaseName.StartsWith(lowerOtherName + "-"))
                return true;

            // Check for common split patterns
            var splitPatterns = new[] {
                "arm64", "arm", "x86", "x86_64",
                "xxhdpi", "xhdpi", "hdpi", "mdpi", "ldpi",
                "nodpi", "tvdpi", "xxxhdpi",
                "v7a", "v8a", "v21", "v22", "v23", "v24", "v25", "v26", "v27", "v28", "v29", "v30", "v31", "v32", "v33", "v34"
            };

            foreach (var pattern in splitPatterns)
            {
                if ((lowerBaseName.EndsWith("-" + pattern) && lowerOtherName.EndsWith("-" + pattern)) ||
                    (lowerBaseName.Contains("-" + pattern + "-") && lowerOtherName.Contains("-" + pattern + "-")))
                    return true;
            }

            // Check if they share the same base name but have different suffixes
            var baseParts = lowerBaseName.Split('-');
            var otherParts = lowerOtherName.Split('-');

            if (baseParts.Length > 1 && otherParts.Length > 1)
            {
                // If they have the same first part (package name), they might be related
                if (baseParts[0] == otherParts[0] && baseParts[0].Contains("."))
                {
                    // Check if the difference is just a split identifier
                    var baseSuffix = string.Join("-", baseParts.Skip(1));
                    var otherSuffix = string.Join("-", otherParts.Skip(1));

                    // If one is empty (base APK) and the other has a suffix (split APK)
                    if (string.IsNullOrEmpty(baseSuffix) && !string.IsNullOrEmpty(otherSuffix))
                        return true;
                    if (!string.IsNullOrEmpty(baseSuffix) && string.IsNullOrEmpty(otherSuffix))
                        return true;
                }
            }

            return false;
        }

        private ApkItem? AnalyzeApkFile(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileName = Path.GetFileName(filePath);
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);

                var apkItem = new ApkItem
                {
                    FilePath = filePath,
                    FileName = fileName,
                    FileSize = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime
                };

                // Determine if this is a base APK or split APK
                var isBase = IsBaseApk(fileNameWithoutExt);
                if (isBase)
                {
                    apkItem.SplitTag = "Base";
                    apkItem.Package = ExtractPackageName(fileNameWithoutExt);
                    Debug.WriteLine($"[APK Analysis] {fileName} -> Base APK (Package: {apkItem.Package})");
                }
                else
                {
                    apkItem.SplitTag = ExtractSplitTag(fileNameWithoutExt);
                    apkItem.Package = ExtractPackageName(fileNameWithoutExt);
                    Debug.WriteLine($"[APK Analysis] {fileName} -> Split APK (Tag: {apkItem.SplitTag}, Package: {apkItem.Package})");
                }

                // Try to extract version and SDK info from filename
                ExtractVersionInfo(fileNameWithoutExt, apkItem);

                return apkItem;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error analyzing APK file {filePath}: {ex.Message}");
                return null;
            }
        }

        private bool IsBaseApk(string fileName)
        {
            // Convert to lowercase for case-insensitive comparison
            var lowerFileName = fileName.ToLowerInvariant();

            // Common split APK patterns that indicate this is NOT a base APK
            var splitPatterns = new[] {
                "arm64", "arm", "x86", "x86_64",
                "xxhdpi", "xhdpi", "hdpi", "mdpi", "ldpi",
                "nodpi", "tvdpi", "xxxhdpi",
                "v7a", "v8a", "v21", "v22", "v23", "v24", "v25", "v26", "v27", "v28", "v29", "v30", "v31", "v32", "v33", "v34",
                "universal", "multi", "split"
            };

            // Check if filename contains any split patterns
            foreach (var pattern in splitPatterns)
            {
                if (lowerFileName.Contains(pattern))
                    return false;
            }

            // Check for common split APK naming conventions
            if (lowerFileName.Contains("-") && lowerFileName.Split('-').Length > 1)
            {
                var parts = lowerFileName.Split('-');
                var lastPart = parts[parts.Length - 1];

                // If the last part looks like a split identifier, it's likely a split APK
                if (lastPart.Length <= 6 && (lastPart.All(char.IsDigit) || lastPart.All(char.IsLetter)))
                {
                    // Additional check: if it ends with numbers or short letters, likely split APK
                    if (lastPart.All(char.IsDigit) || (lastPart.Length <= 4 && lastPart.All(char.IsLetter)))
                        return false;
                }
            }

            // Check if it's a simple name without dashes (likely base APK)
            if (!lowerFileName.Contains("-"))
                return true;

            // Check if it's just a package name with version (likely base APK)
            var dashParts = lowerFileName.Split('-');
            if (dashParts.Length == 1)
                return true;

            // If it has version number at the end, it might be base APK
            if (dashParts.Length == 2)
            {
                var secondPart = dashParts[1];
                // Check if second part looks like a version number
                if (secondPart.Contains(".") || secondPart.All(char.IsDigit) ||
                    (secondPart.StartsWith("v") && secondPart.Substring(1).All(char.IsDigit)))
                    return true;
            }

            // Default to split APK if we're unsure
            return false;
        }

        private string ExtractPackageName(string fileName)
        {
            // Try to extract package name from filename
            // Common patterns: com.example.app, com.example.app-v1, etc.
            var parts = fileName.Split('-');
            if (parts.Length > 0)
            {
                var firstPart = parts[0];

                // Check if it looks like a package name (contains dots and is reasonably long)
                if (firstPart.Contains(".") && firstPart.Length > 3)
                {
                    // Validate package name format
                    var packageParts = firstPart.Split('.');
                    if (packageParts.Length >= 2)
                    {
                        // Check if all parts are valid package name components
                        bool isValidPackage = true;
                        foreach (var part in packageParts)
                        {
                            if (string.IsNullOrEmpty(part) || !part.All(c => char.IsLetterOrDigit(c) || c == '_'))
                            {
                                isValidPackage = false;
                                break;
                            }
                        }

                        if (isValidPackage)
                            return firstPart;
                    }
                }

                // If first part doesn't look like a package name, try to find one
                foreach (var part in parts)
                {
                    if (part.Contains(".") && part.Length > 3)
                    {
                        var packageParts = part.Split('.');
                        if (packageParts.Length >= 2)
                        {
                            bool isValidPackage = true;
                            foreach (var packagePart in packageParts)
                            {
                                if (string.IsNullOrEmpty(packagePart) || !packagePart.All(c => char.IsLetterOrDigit(c) || c == '_'))
                                {
                                    isValidPackage = false;
                                    break;
                                }
                            }

                            if (isValidPackage)
                                return part;
                        }
                    }
                }
            }

            // If no package name found, return a cleaned version of the filename
            var cleanName = fileName.Replace("-", "_").Replace(" ", "_");
            return cleanName.Length > 20 ? cleanName.Substring(0, 20) : cleanName;
        }

        private string ExtractSplitTag(string fileName)
        {
            // Convert to lowercase for case-insensitive comparison
            var lowerFileName = fileName.ToLowerInvariant();

            // Extract split tag from filename
            var parts = fileName.Split('-');
            if (parts.Length > 1)
            {
                // Look for common split patterns
                var splitPatterns = new[] {
                    "arm64", "arm", "x86", "x86_64",
                    "xxhdpi", "xhdpi", "hdpi", "mdpi", "ldpi",
                    "nodpi", "tvdpi", "xxxhdpi",
                    "v7a", "v8a", "v21", "v22", "v23", "v24", "v25", "v26", "v27", "v28", "v29", "v30", "v31", "v32", "v33", "v34"
                };

                foreach (var pattern in splitPatterns)
                {
                    if (lowerFileName.Contains(pattern))
                    {
                        // Return the pattern in proper case
                        var index = lowerFileName.IndexOf(pattern);
                        if (index >= 0)
                        {
                            var actualPattern = fileName.Substring(index, pattern.Length);
                            return actualPattern.ToUpper();
                        }
                    }
                }

                // Check the last part for split identifiers
                var lastPart = parts[parts.Length - 1];
                if (lastPart.Length <= 6)
                {
                    // If it's all digits, it might be a version or split number
                    if (lastPart.All(char.IsDigit))
                        return $"SPLIT_{lastPart}";

                    // If it's all letters and short, it might be a split identifier
                    if (lastPart.All(char.IsLetter) && lastPart.Length <= 4)
                        return lastPart.ToUpper();
                }

                // Return the last part if no common pattern found
                return parts[parts.Length - 1].ToUpper();
            }

            return "Split";
        }

        private void ExtractVersionInfo(string fileName, ApkItem apkItem)
        {
            // Try to extract version info from filename
            // Common patterns: app-v1.2.3, app-123, etc.
            var versionMatch = Regex.Match(fileName, @"[vV]?(\d+(?:\.\d+)*)");
            if (versionMatch.Success)
            {
                apkItem.Version = versionMatch.Groups[1].Value;
            }

            // Try to extract SDK info
            var sdkMatch = Regex.Match(fileName, @"api-(\d+)");
            if (sdkMatch.Success)
            {
                apkItem.TargetSdk = sdkMatch.Groups[1].Value;
            }
        }

        public void Dispose() => _watcher?.Dispose();
    }
}