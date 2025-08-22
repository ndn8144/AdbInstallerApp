using AdbInstallerApp.Models;
using System.Text.RegularExpressions;

namespace AdbInstallerApp.Services
{
    public class DeviceCompatibilityAnalyzer
    {
        public CompatibilityResult CheckCompatibility(DeviceInfo device, ApkGroup apkGroup)
        {
            var result = new CompatibilityResult();
            
            if (apkGroup.BaseApk == null)
            {
                result.IsCompatible = false;
                result.Reason = "No base APK found";
                return result;
            }
            
            // Check SDK compatibility
            if (!CheckSdkCompatibility(device, apkGroup.BaseApk))
            {
                result.IsCompatible = false;
                result.Reason = "SDK version incompatible";
                return result;
            }
            
            // Check ABI compatibility
            var deviceAbis = ParseAbiList(device.Abi);
            var recommendedSplits = apkGroup.SplitApks
                .Where(split => IsAbiCompatible(split, deviceAbis))
                .ToList();
                
            result.RecommendedSplits = recommendedSplits;
            
            // Check DPI compatibility  
            var deviceDpi = ParseDensity(device.Density);
            var dpiSplit = FindBestDpiSplit(apkGroup.SplitApks, deviceDpi);
            result.OptimalDpi = dpiSplit;
            
            // Calculate compatibility score
            result.CompatibilityScore = CalculateScore(device, apkGroup, recommendedSplits, dpiSplit);
            result.IsCompatible = result.CompatibilityScore >= 0.7; // 70% threshold
            
            return result;
        }
        
        private bool CheckSdkCompatibility(DeviceInfo device, ApkItem apk)
        {
            if (string.IsNullOrEmpty(device.Sdk) || string.IsNullOrEmpty(apk.MinSdk))
                return true; // Assume compatible if info missing
                
            if (int.TryParse(device.Sdk, out var deviceSdk) && 
                int.TryParse(apk.MinSdk, out var minSdk))
            {
                return deviceSdk >= minSdk;
            }
            
            return true;
        }
        
        private List<string> ParseAbiList(string deviceAbi)
        {
            if (string.IsNullOrEmpty(deviceAbi))
                return new List<string>();
                
            // Parse ABI string like "arm64-v8a,armeabi-v7a,armeabi"
            return deviceAbi.Split(',')
                .Select(abi => abi.Trim().ToLower())
                .ToList();
        }
        
        private bool IsAbiCompatible(ApkItem split, List<string> deviceAbis)
        {
            if (split.Type != ApkType.SplitAbi || string.IsNullOrEmpty(split.SplitTag))
                return false;
                
            // Check if split ABI matches device ABI
            var splitAbi = split.SplitTag.ToLower();
            return deviceAbis.Any(deviceAbi => 
                deviceAbi.Contains(splitAbi) || splitAbi.Contains(deviceAbi));
        }
        
        private int ParseDensity(string density)
        {
            if (string.IsNullOrEmpty(density))
                return 0;
                
            // Extract numeric value from density string
            var match = Regex.Match(density, @"(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var dpi))
            {
                return dpi;
            }
            
            return 0;
        }
        
        private ApkItem? FindBestDpiSplit(List<ApkItem> splits, int deviceDpi)
        {
            if (deviceDpi == 0)
                return null;
                
            var dpiSplits = splits.Where(s => s.Type == ApkType.SplitDpi).ToList();
            if (!dpiSplits.Any())
                return null;
                
            // Find closest DPI match
            ApkItem? bestMatch = null;
            var bestDifference = int.MaxValue;
            
            foreach (var split in dpiSplits)
            {
                var splitDpi = ParseDensity(split.SplitTag);
                if (splitDpi > 0)
                {
                    var difference = Math.Abs(deviceDpi - splitDpi);
                    if (difference < bestDifference)
                    {
                        bestDifference = difference;
                        bestMatch = split;
                    }
                }
            }
            
            return bestMatch;
        }
        
        private double CalculateScore(DeviceInfo device, ApkGroup apkGroup, 
            List<ApkItem> recommendedSplits, ApkItem? dpiSplit)
        {
            var score = 1.0;
            
            // Base APK compatibility
            if (apkGroup.BaseApk != null)
                score *= 1.0;
            else
                score *= 0.0;
                
            // ABI compatibility
            var abiScore = recommendedSplits.Count > 0 ? 1.0 : 0.5;
            score *= abiScore;
            
            // DPI compatibility
            var dpiScore = dpiSplit != null ? 1.0 : 0.8;
            score *= dpiScore;
            
            // SDK compatibility
            if (apkGroup.BaseApk != null && CheckSdkCompatibility(device, apkGroup.BaseApk))
                score *= 1.0;
            else
                score *= 0.0;
                
            return Math.Max(0.0, Math.Min(1.0, score));
        }
    }
    
    public class CompatibilityResult
    {
        public bool IsCompatible { get; set; }
        public double CompatibilityScore { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<ApkItem> RecommendedSplits { get; set; } = new List<ApkItem>();
        public ApkItem? OptimalDpi { get; set; }
        
        public string GetCompatibilityText()
        {
            if (IsCompatible)
            {
                var parts = new List<string>();
                if (RecommendedSplits.Count > 0)
                    parts.Add($"{RecommendedSplits.Count} ABI splits");
                if (OptimalDpi != null)
                    parts.Add("DPI optimized");
                    
                var details = parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "";
                return $"✅ Compatible{details}";
            }
            else
            {
                return $"❌ Not compatible: {Reason}";
            }
        }
    }
}
