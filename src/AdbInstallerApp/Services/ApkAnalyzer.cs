using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AdbInstallerApp.Models;

namespace AdbInstallerApp.Services
{
    /// <summary>
    /// Service for analyzing APK files and extracting metadata
    /// </summary>
    public class ApkAnalyzer
    {
        private readonly string _aaptPath;
        
        public ApkAnalyzer(string adbToolsPath)
        {
            _aaptPath = Path.Combine(adbToolsPath, "aapt.exe");
        }
        
        /// <summary>
        /// Analyze APK file and extract metadata
        /// </summary>
        public async Task<ApkFile?> AnalyzeApkAsync(string apkPath)
        {
            try
            {
                if (!File.Exists(apkPath))
                    return null;
                    
                var info = await ExtractApkInfoAsync(apkPath);
                if (info == null)
                    return null;
                
                return new ApkFile(
                    Path: apkPath,
                    PackageName: info.PackageName,
                    IsBase: info.IsBase,
                    Abi: info.Abi,
                    Dpi: info.Dpi,
                    Locale: info.Locale,
                    VersionCode: info.VersionCode
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error analyzing APK {apkPath}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Group APK files by package name and create installation units
        /// </summary>
        public List<InstallationUnit> GroupApksByPackage(IEnumerable<ApkFile> apkFiles)
        {
            var result = new List<InstallationUnit>();
            
            // Group by package name
            var packageGroups = apkFiles
                .Where(apk => apk.IsValid)
                .GroupBy(apk => apk.PackageName)
                .ToList();
            
            foreach (var group in packageGroups)
            {
                var files = group.ToList();
                
                // Check if we have a base APK
                var baseApk = files.FirstOrDefault(f => f.IsBase);
                
                if (files.Count == 1 && baseApk == null)
                {
                    // Single APK file (treat as base)
                    var singleApk = files[0] with { IsBase = true };
                    result.Add(new InstallationUnit(group.Key, new[] { singleApk }));
                }
                else if (baseApk != null)
                {
                    // APK bundle with base + splits
                    result.Add(new InstallationUnit(group.Key, files));
                }
                else
                {
                    // Invalid group - no base APK found
                    System.Diagnostics.Debug.WriteLine($"Warning: Package {group.Key} has no base APK");
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Match split APKs to device capabilities
        /// </summary>
        public InstallationUnit MatchSplitsToDevice(InstallationUnit unit, DeviceProps deviceProps)
        {
            if (!unit.IsGroup)
                return unit; // Single APK, no matching needed
                
            var baseApk = unit.BaseApk;
            if (baseApk == null)
                return unit; // Invalid unit
                
            var matchedFiles = new List<ApkFile> { baseApk };
            var splitApks = unit.SplitApks.ToList();
            
            // Match ABI splits
            var abiMatches = splitApks.Where(split => 
                string.IsNullOrEmpty(split.Abi) || 
                deviceProps.SupportedAbis.Contains(split.Abi, StringComparer.OrdinalIgnoreCase))
                .ToList();
                
            // Match DPI splits
            var dpiMatches = abiMatches.Where(split =>
                string.IsNullOrEmpty(split.Dpi) ||
                IsDpiCompatible(split.Dpi, deviceProps.Density))
                .ToList();
                
            // Match locale splits (for now, include all locale splits)
            var localeMatches = dpiMatches.Where(split =>
                string.IsNullOrEmpty(split.Locale) ||
                IsLocaleCompatible(split.Locale))
                .ToList();
                
            matchedFiles.AddRange(localeMatches);
            
            return new InstallationUnit(unit.PackageName, matchedFiles);
        }
        
        private bool IsDpiCompatible(string splitDpi, int deviceDensity)
        {
            return splitDpi.ToLowerInvariant() switch
            {
                "ldpi" => deviceDensity <= 140,
                "mdpi" => deviceDensity > 140 && deviceDensity <= 200,
                "hdpi" => deviceDensity > 200 && deviceDensity <= 280,
                "xhdpi" => deviceDensity > 280 && deviceDensity <= 400,
                "xxhdpi" => deviceDensity > 400 && deviceDensity <= 560,
                "xxxhdpi" => deviceDensity > 560,
                "nodpi" => true, // No DPI restriction
                _ => true // Unknown DPI, assume compatible
            };
        }
        
        private bool IsLocaleCompatible(string locale)
        {
            // For now, accept all locales
            // Could be enhanced to match system locale
            return true;
        }
        
        private async Task<ApkInfo?> ExtractApkInfoAsync(string apkPath)
        {
            try
            {
                // Use aapt to extract APK information
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _aaptPath,
                        Arguments = $"dump badging \"{apkPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    // Fallback: try to extract info from filename
                    return ExtractInfoFromFilename(apkPath);
                }
                
                return ParseAaptOutput(output, apkPath);
            }
            catch
            {
                // Fallback: extract info from filename
                return ExtractInfoFromFilename(apkPath);
            }
        }
        
        private ApkInfo ParseAaptOutput(string aaptOutput, string apkPath)
        {
            var info = new ApkInfo();
            
            // Extract package name
            var packageMatch = Regex.Match(aaptOutput, @"package: name='([^']+)'");
            if (packageMatch.Success)
                info.PackageName = packageMatch.Groups[1].Value;
            
            // Extract version code
            var versionMatch = Regex.Match(aaptOutput, @"versionCode='(\d+)'");
            if (versionMatch.Success && long.TryParse(versionMatch.Groups[1].Value, out var versionCode))
                info.VersionCode = versionCode;
            
            // Check if it's a base APK (has application tag)
            info.IsBase = aaptOutput.Contains("application:");
            
            // Extract split info from filename if not base
            if (!info.IsBase)
            {
                var filename = Path.GetFileNameWithoutExtension(apkPath);
                ExtractSplitInfoFromFilename(filename, info);
            }
            
            // Fallback to filename parsing if package name not found
            if (string.IsNullOrEmpty(info.PackageName))
            {
                var fallback = ExtractInfoFromFilename(apkPath);
                if (fallback != null)
                {
                    info.PackageName = fallback.PackageName;
                    info.Abi = fallback.Abi;
                    info.Dpi = fallback.Dpi;
                    info.Locale = fallback.Locale;
                }
            }
            
            return info;
        }
        
        private ApkInfo? ExtractInfoFromFilename(string apkPath)
        {
            var filename = Path.GetFileNameWithoutExtension(apkPath);
            var info = new ApkInfo();
            
            // Common patterns for APK filenames:
            // app-release.apk -> base APK
            // app-arm64-v8a-release.apk -> ABI split
            // app-xxhdpi-release.apk -> DPI split
            // app-en-release.apk -> locale split
            
            // Extract package name (first part before version or split info)
            var parts = filename.Split('-', '_', '.');
            if (parts.Length > 0)
            {
                info.PackageName = parts[0];
            }
            
            // Check for base indicators
            info.IsBase = filename.Contains("base") || 
                         filename.Contains("release") && !ContainsSplitIndicators(filename) ||
                         !ContainsSplitIndicators(filename);
            
            // Extract split information
            ExtractSplitInfoFromFilename(filename, info);
            
            return string.IsNullOrEmpty(info.PackageName) ? null : info;
        }
        
        private void ExtractSplitInfoFromFilename(string filename, ApkInfo info)
        {
            var lower = filename.ToLowerInvariant();
            
            // ABI patterns
            var abiPatterns = new[] { "arm64-v8a", "armeabi-v7a", "armeabi", "x86_64", "x86", "mips64", "mips" };
            info.Abi = abiPatterns.FirstOrDefault(abi => lower.Contains(abi));
            
            // DPI patterns
            var dpiPatterns = new[] { "xxxhdpi", "xxhdpi", "xhdpi", "hdpi", "mdpi", "ldpi", "nodpi" };
            info.Dpi = dpiPatterns.FirstOrDefault(dpi => lower.Contains(dpi));
            
            // Locale patterns (simplified)
            var localePatterns = new[] { "en", "es", "fr", "de", "it", "pt", "ru", "ja", "ko", "zh" };
            info.Locale = localePatterns.FirstOrDefault(locale => 
                lower.Contains($"-{locale}-") || lower.Contains($"_{locale}_") || lower.EndsWith($"-{locale}"));
        }
        
        private bool ContainsSplitIndicators(string filename)
        {
            var lower = filename.ToLowerInvariant();
            var indicators = new[] { "arm", "x86", "mips", "hdpi", "dpi", "en-", "es-", "fr-", "de-" };
            return indicators.Any(indicator => lower.Contains(indicator));
        }
        
        private class ApkInfo
        {
            public string PackageName { get; set; } = "";
            public bool IsBase { get; set; } = true;
            public string? Abi { get; set; }
            public string? Dpi { get; set; }
            public string? Locale { get; set; }
            public long VersionCode { get; set; }
        }
    }
}
