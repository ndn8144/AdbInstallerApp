using AdbInstallerApp.Helpers;
using AdbInstallerApp.Models;
using System.Text.RegularExpressions;
using System.IO;

namespace AdbInstallerApp.Services
{
    public class ApkAnalyzer
    {
        private readonly string _aaptPath;
        
        public ApkAnalyzer()
        {
            // Tìm aapt trong thư mục build-tools
            _aaptPath = FindAaptPath();
        }
        
        public async Task<ApkManifest> ParseManifestAsync(string apkPath)
        {
            try
            {
                if (string.IsNullOrEmpty(_aaptPath))
                {
                    throw new InvalidOperationException("aapt tool not found. Please ensure Android SDK build-tools are installed.");
                }

                // Sử dụng aapt dump badging để lấy thông tin
                var (code, output, _) = await ProcessRunner.RunAsync(
                    _aaptPath, $"dump badging \"{apkPath}\"");
                    
                if (code != 0)
                {
                    throw new InvalidOperationException($"aapt failed with exit code {code}");
                }
                
                return ParseAaptOutput(output);
            }
            catch (Exception)
            {
                // Trả về manifest rỗng nếu có lỗi
                return new ApkManifest();
            }
        }
        
        public ApkType DetectApkType(string fileName, ApkManifest manifest)
        {
            // Phân loại: Base APK, Split APK (ABI/DPI/Language)
            if (manifest.IsSplit) 
            {
                if (fileName.Contains("arm64") || fileName.Contains("x86") || 
                    fileName.Contains("arm") || fileName.Contains("x86_64")) 
                    return ApkType.SplitAbi;
                if (fileName.Contains("xxhdpi") || fileName.Contains("xxxhdpi") ||
                    fileName.Contains("hdpi") || fileName.Contains("mdpi") ||
                    fileName.Contains("xhdpi") || fileName.Contains("ldpi"))
                    return ApkType.SplitDpi;
                if (fileName.Contains("en") || fileName.Contains("vi") ||
                    fileName.Contains("fr") || fileName.Contains("de") ||
                    fileName.Contains("es") || fileName.Contains("ja") ||
                    fileName.Contains("ko") || fileName.Contains("zh"))
                    return ApkType.SplitLanguage;
                return ApkType.SplitOther;
            }
            return ApkType.Base;
        }
        
        private ApkManifest ParseAaptOutput(string output)
        {
            var manifest = new ApkManifest();
            
            if (string.IsNullOrEmpty(output))
                return manifest;
                
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Package info
                if (trimmedLine.StartsWith("package:"))
                {
                    var match = Regex.Match(trimmedLine, @"package: name='([^']+)' versionCode='([^']+)' versionName='([^']+)'");
                    if (match.Success)
                    {
                        manifest.PackageName = match.Groups[1].Value;
                        manifest.VersionCode = match.Groups[2].Value;
                        manifest.VersionName = match.Groups[3].Value;
                    }
                }
                
                // Application label
                else if (trimmedLine.StartsWith("application-label:"))
                {
                    var match = Regex.Match(trimmedLine, @"application-label:'([^']+)'");
                    if (match.Success)
                    {
                        manifest.AppLabel = match.Groups[1].Value;
                    }
                }
                
                // SDK versions
                else if (trimmedLine.StartsWith("sdkVersion:"))
                {
                    var match = Regex.Match(trimmedLine, @"sdkVersion:'([^']+)'");
                    if (match.Success)
                    {
                        manifest.TargetSdk = match.Groups[1].Value;
                    }
                }
                
                else if (trimmedLine.StartsWith("minSdkVersion:"))
                {
                    var match = Regex.Match(trimmedLine, @"minSdkVersion:'([^']+)'");
                    if (match.Success)
                    {
                        manifest.MinSdk = match.Groups[1].Value;
                    }
                }
                
                // Native code (ABI)
                else if (trimmedLine.StartsWith("native-code:"))
                {
                    var match = Regex.Match(trimmedLine, @"native-code:'([^']+)'");
                    if (match.Success)
                    {
                        manifest.NativeCode = match.Groups[1].Value;
                        manifest.SupportedAbis = match.Groups[1].Value.Split(' ').ToList();
                    }
                }
                
                // Split info
                else if (trimmedLine.StartsWith("split:"))
                {
                    manifest.IsSplit = true;
                    var match = Regex.Match(trimmedLine, @"split:'([^']+)'");
                    if (match.Success)
                    {
                        manifest.SplitName = match.Groups[1].Value;
                    }
                }
                
                // Permissions
                else if (trimmedLine.StartsWith("uses-permission:"))
                {
                    var match = Regex.Match(trimmedLine, @"uses-permission:'([^']+)'");
                    if (match.Success)
                    {
                        manifest.Permissions.Add(match.Groups[1].Value);
                    }
                }
                
                // Features
                else if (trimmedLine.StartsWith("uses-feature:"))
                {
                    var match = Regex.Match(trimmedLine, @"uses-feature:'([^']+)'");
                    if (match.Success)
                    {
                        manifest.Features.Add(match.Groups[1].Value);
                    }
                }
            }
            
            return manifest;
        }
        
        private string FindAaptPath()
        {
            // Tìm aapt trong thư mục build-tools
            var buildToolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "tools", "build-tools");
            
            if (Directory.Exists(buildToolsPath))
            {
                var buildToolDirs = Directory.GetDirectories(buildToolsPath)
                    .Where(d => Path.GetFileName(d).Contains("."))
                    .OrderByDescending(d => Path.GetFileName(d))
                    .ToList();
                    
                foreach (var dir in buildToolDirs)
                {
                    var aaptPath = Path.Combine(dir, "aapt.exe");
                    if (File.Exists(aaptPath))
                    {
                        return aaptPath;
                    }
                }
            }
            
            // Fallback: tìm trong PATH
            return "aapt";
        }
    }
}
