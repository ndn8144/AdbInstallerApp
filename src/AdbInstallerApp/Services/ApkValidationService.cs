using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace AdbInstallerApp.Services
{
    public class ApkValidationService
    {
        public class ApkValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
            public List<string> Warnings { get; set; } = new List<string>();
            public ApkInfo? ApkInfo { get; set; }
        }

        public class ApkInfo
        {
            public string PackageName { get; set; } = string.Empty;
            public string VersionName { get; set; } = string.Empty;
            public int VersionCode { get; set; }
            public int MinSdkVersion { get; set; }
            public int TargetSdkVersion { get; set; }
            public List<string> Permissions { get; set; } = new List<string>();
            public bool HasSplits { get; set; }
            public long FileSize { get; set; }
            public bool HasNativeLibraries { get; set; }
            public List<string> NativeLibraryPaths { get; set; } = new List<string>();
            public List<string> SupportedArchitectures { get; set; } = new List<string>();
        }

        public async Task<ApkValidationResult> ValidateApkAsync(string apkPath)
        {
            var result = new ApkValidationResult();

            try
            {
                // Basic file checks
                if (!File.Exists(apkPath))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "APK file does not exist";
                    return result;
                }

                var fileInfo = new FileInfo(apkPath);
                if (fileInfo.Length == 0)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "APK file is empty";
                    return result;
                }

                if (fileInfo.Length < 1024) // Less than 1KB is suspicious
                {
                    result.Warnings.Add("APK file is unusually small, may be corrupted");
                }

                // Try to open as ZIP file
                using var zip = ZipFile.OpenRead(apkPath);
                
                // Check for essential APK components
                var entries = zip.Entries.ToList();
                
                var hasAndroidManifest = entries.Any(e => e.Name == "AndroidManifest.xml");
                var hasResources = entries.Any(e => e.Name == "resources.arsc");
                var hasDex = entries.Any(e => e.Name.EndsWith(".dex"));
                var hasMetaInf = entries.Any(e => e.FullName.StartsWith("META-INF/"));
                
                if (!hasAndroidManifest)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Missing AndroidManifest.xml - not a valid APK";
                    return result;
                }

                if (!hasDex)
                {
                    result.Warnings.Add("No .dex files found - APK may not contain executable code");
                }

                if (!hasResources)
                {
                    result.Warnings.Add("No resources.arsc found - APK may be incomplete");
                }

                // Check for split APK indicators
                var splitEntries = entries.Where(e => e.Name.Contains("split_") || e.Name.Contains("config.")).ToList();
                result.ApkInfo = new ApkInfo { HasSplits = splitEntries.Count > 0, FileSize = fileInfo.Length };

                // Check for native libraries and architecture support
                var nativeLibEntries = entries.Where(e => e.FullName.StartsWith("lib/")).ToList();
                result.ApkInfo.HasNativeLibraries = nativeLibEntries.Any();
                result.ApkInfo.NativeLibraryPaths = nativeLibEntries.Select(e => e.FullName).ToList();

                // Extract supported architectures from lib/ folder
                var architectures = new HashSet<string>();
                foreach (var libEntry in nativeLibEntries)
                {
                    var pathParts = libEntry.FullName.Split('/');
                    if (pathParts.Length >= 2)
                    {
                        var arch = pathParts[1];
                        if (IsValidArchitecture(arch))
                        {
                            architectures.Add(arch);
                        }
                    }
                }
                result.ApkInfo.SupportedArchitectures = architectures.ToList();

                // Add warnings for architecture issues
                if (result.ApkInfo.HasNativeLibraries && result.ApkInfo.SupportedArchitectures.Count == 0)
                {
                    result.Warnings.Add("APK has native libraries but architecture could not be determined");
                }
                else if (result.ApkInfo.HasNativeLibraries)
                {
                    result.Warnings.Add($"APK supports architectures: {string.Join(", ", result.ApkInfo.SupportedArchitectures)}");
                }

                // Try to extract basic info from AndroidManifest.xml
                try
                {
                    var manifestEntry = entries.FirstOrDefault(e => e.Name == "AndroidManifest.xml");
                    if (manifestEntry != null)
                    {
                        using var stream = manifestEntry.Open();
                        using var reader = new StreamReader(stream);
                        var manifestContent = await reader.ReadToEndAsync();
                        
                        // Basic parsing (simplified)
                        result.ApkInfo.PackageName = ExtractPackageName(manifestContent);
                        result.ApkInfo.VersionName = ExtractVersionName(manifestContent);
                        result.ApkInfo.VersionCode = ExtractVersionCode(manifestContent);
                        result.ApkInfo.MinSdkVersion = ExtractMinSdkVersion(manifestContent);
                        result.ApkInfo.TargetSdkVersion = ExtractTargetSdkVersion(manifestContent);
                        result.ApkInfo.Permissions = ExtractPermissions(manifestContent);
                    }
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Could not parse AndroidManifest.xml: {ex.Message}");
                }

                // Check for potential corruption indicators
                var corruptedEntries = entries.Where(e => e.Length == 0 && e.Name != "META-INF/MANIFEST.MF").ToList();
                if (corruptedEntries.Any())
                {
                    result.Warnings.Add($"Found {corruptedEntries.Count} potentially corrupted entries");
                }

                // Check for duplicate entries (common cause of "Split null was defined multiple times")
                var duplicateNames = entries.GroupBy(e => e.Name).Where(g => g.Count() > 1).ToList();
                if (duplicateNames.Any())
                {
                    result.Warnings.Add($"Found {duplicateNames.Count} duplicate entry names - this may cause installation issues");
                }

                result.IsValid = true;
                return result;
            }
            catch (InvalidDataException)
            {
                result.IsValid = false;
                result.ErrorMessage = "APK file is corrupted or not a valid ZIP archive";
                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Error validating APK: {ex.Message}";
                return result;
            }
        }

        private string ExtractPackageName(string manifestContent)
        {
            var match = Regex.Match(manifestContent, @"package=[""']([^""']+)[""']");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private string ExtractVersionName(string manifestContent)
        {
            var match = Regex.Match(manifestContent, @"android:versionName=[""']([^""']+)[""']");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private int ExtractVersionCode(string manifestContent)
        {
            var match = Regex.Match(manifestContent, @"android:versionCode=[""']([^""']+)[""']");
            return match.Success && int.TryParse(match.Groups[1].Value, out var code) ? code : 0;
        }

        private int ExtractMinSdkVersion(string manifestContent)
        {
            var match = Regex.Match(manifestContent, @"android:minSdkVersion=[""']([^""']+)[""']");
            return match.Success && int.TryParse(match.Groups[1].Value, out var code) ? code : 0;
        }

        private int ExtractTargetSdkVersion(string manifestContent)
        {
            var match = Regex.Match(manifestContent, @"android:targetSdkVersion=[""']([^""']+)[""']");
            return match.Success && int.TryParse(match.Groups[1].Value, out var code) ? code : 0;
        }

        private List<string> ExtractPermissions(string manifestContent)
        {
            var permissions = new List<string>();
            var matches = Regex.Matches(manifestContent, @"<uses-permission\s+android:name=[""']([^""']+)[""']");
            
            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    permissions.Add(match.Groups[1].Value);
                }
            }
            
            return permissions;
        }

        private bool IsValidArchitecture(string architecture)
        {
            var validArchs = new[] { "arm64-v8a", "armeabi-v7a", "armeabi", "x86", "x86_64", "mips", "mips64" };
            return validArchs.Contains(architecture.ToLower());
        }

        public Task<bool> TryRepairApkAsync(string apkPath, string outputPath)
        {
            try
            {
                // Create a copy and try to repair common issues
                File.Copy(apkPath, outputPath, true);
                
                using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Update);
                
                // Remove duplicate entries
                var entries = zip.Entries.ToList();
                var duplicateGroups = entries.GroupBy(e => e.Name).Where(g => g.Count() > 1);
                
                foreach (var group in duplicateGroups)
                {
                    var duplicates = group.Skip(1).ToList(); // Keep first, remove rest
                    foreach (var duplicate in duplicates)
                    {
                        duplicate.Delete();
                    }
                }
                
                // Remove corrupted entries
                var corruptedEntries = entries.Where(e => e.Length == 0 && e.Name != "META-INF/MANIFEST.MF").ToList();
                foreach (var entry in corruptedEntries)
                {
                    entry.Delete();
                }
                
                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }
}
