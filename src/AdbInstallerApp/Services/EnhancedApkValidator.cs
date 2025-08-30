using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AdbInstallerApp.Models;

namespace AdbInstallerApp.Services
{
    /// <summary>
    /// Enhanced APK validation service with conflict detection and device compatibility
    /// </summary>
    public sealed class EnhancedApkValidator
    {
        private readonly ApkAnalyzer _apkAnalyzer;
        
        public EnhancedApkValidator(string adbToolsPath)
        {
            _apkAnalyzer = new ApkAnalyzer(adbToolsPath);
        }

        /// <summary>
        /// Build group snapshots from selected file paths with validation
        /// </summary>
        public async Task<List<GroupSnapshot>> BuildGroupsAsync(
            IEnumerable<string> selectedPaths, 
            CancellationToken cancellationToken = default)
        {
            var apkFiles = new List<ApkFile>();
            
            // Step 1: Analyze all APK files and create snapshots
            foreach (var path in selectedPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (!File.Exists(path) || !path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                    continue;
                    
                try
                {
                    var apkFile = await AnalyzeApkFileAsync(path, cancellationToken);
                    if (apkFile != null)
                        apkFiles.Add(apkFile);
                }
                catch (Exception ex)
                {
                    // Log but continue with other files
                    System.Diagnostics.Debug.WriteLine($"Failed to analyze {path}: {ex.Message}");
                }
            }
            
            // Step 2: Group by package name
            var groups = apkFiles.GroupBy(f => f.PackageName, StringComparer.OrdinalIgnoreCase)
                                .Select(g => new GroupSnapshot(g.Key, g.ToList(), DateTime.Now))
                                .ToList();
            
            return groups;
        }

        /// <summary>
        /// Validate group and detect conflicts
        /// </summary>
        public ValidationResult ValidateGroup(GroupSnapshot group, DeviceInstallOptions options)
        {
            return group.Validate(options);
        }

        /// <summary>
        /// Pick the best base APK for device according to options
        /// </summary>
        public ApkFile? PickBase(IReadOnlyList<ApkFile> bases, DeviceInstallOptions options, int deviceSdk)
        {
            if (bases.Count == 0) return null;
            if (bases.Count == 1) return bases[0];
            
            // Filter by SDK compatibility first
            var compatibleBases = bases.Where(b => b.IsCompatibleWithSdk(deviceSdk)).ToList();
            if (compatibleBases.Count == 0)
            {
                // No compatible bases - return null to skip this unit on this device
                return null;
            }
            
            if (options.PreferHighestCompatible)
            {
                // Select highest versionCode, respecting AllowDowngrade
                return compatibleBases.OrderByDescending(b => b.VersionCode).First();
            }
            else
            {
                // Select first compatible base
                return compatibleBases.First();
            }
        }

        /// <summary>
        /// Select splits for device based on compatibility and options
        /// </summary>
        public IReadOnlyList<ApkFile> SelectSplitsForDevice(
            GroupSnapshot group, 
            ApkFile baseApk, 
            DeviceProps deviceProps, 
            DeviceInstallOptions options)
        {
            var selectedSplits = new List<ApkFile>();
            var availableSplits = group.Splits.ToList();
            
            // Step 1: Filter by ABI compatibility
            var abiCompatibleSplits = availableSplits.Where(s => 
                string.IsNullOrEmpty(s.Abi) || deviceProps.SupportsAbi(s.Abi)).ToList();
            
            // Step 2: Filter by density compatibility  
            var densityCompatibleSplits = abiCompatibleSplits.Where(s =>
                string.IsNullOrEmpty(s.Dpi) || deviceProps.MatchesDensity(s.Dpi)).ToList();
            
            // Step 3: Filter by locale (if specified)
            var localeCompatibleSplits = densityCompatibleSplits.Where(s =>
                string.IsNullOrEmpty(s.Locale) || 
                string.IsNullOrEmpty(deviceProps.Locale) ||
                string.Equals(s.Locale, deviceProps.Locale, StringComparison.OrdinalIgnoreCase)).ToList();
            
            // Step 4: Apply StrictSplitMatch policy
            switch (options.StrictSplitMatch)
            {
                case StrictSplitMatchMode.Strict:
                    // Require all mandatory splits
                    selectedSplits = ValidateRequiredSplits(baseApk, localeCompatibleSplits);
                    break;
                    
                case StrictSplitMatchMode.Relaxed:
                    // Use all compatible splits, skip incompatible ones
                    selectedSplits = localeCompatibleSplits;
                    break;
                    
                case StrictSplitMatchMode.BaseOnlyFallback:
                    // Try compatible splits, fall back to base-only if issues
                    selectedSplits = localeCompatibleSplits;
                    if (HasSplitConflicts(selectedSplits))
                    {
                        selectedSplits.Clear(); // Base-only fallback
                    }
                    break;
            }
            
            // Step 5: Order files properly (base first, then splits by priority)
            return OrderFiles(baseApk, selectedSplits);
        }

        /// <summary>
        /// Analyze single APK file and extract metadata
        /// </summary>
        private async Task<ApkFile?> AnalyzeApkFileAsync(string path, CancellationToken cancellationToken)
        {
            try
            {
                var fileInfo = new FileInfo(path);
                if (!fileInfo.Exists || fileInfo.Length == 0)
                    return null;
                
                // Calculate SHA256 hash
                var sha256 = await ComputeSha256Async(path, cancellationToken);
                
                // Use existing ApkAnalyzer to get metadata
                var analyzed = await _apkAnalyzer.AnalyzeApkAsync(path);
                if (analyzed == null)
                    return null;
                
                // Extract additional metadata using aapt (if available)
                var metadata = await ExtractApkMetadataAsync(path, cancellationToken);
                
                return new ApkFile(
                    Path: path,
                    PackageName: analyzed.PackageName,
                    IsBase: analyzed.IsBase,
                    Abi: analyzed.Abi,
                    Dpi: analyzed.Dpi,
                    Locale: analyzed.Locale,
                    VersionCode: analyzed.VersionCode,
                    SplitName: analyzed.SplitName,
                    Sha256: sha256,
                    SizeBytes: fileInfo.Length,
                    SignerDigest: metadata.SignerDigest,
                    MinSdk: metadata.MinSdk,
                    TargetSdk: metadata.TargetSdk,
                    RequiredSplits: metadata.RequiredSplits,
                    RequiredFeatures: metadata.RequiredFeatures
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to analyze APK {path}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Compute SHA256 hash of file
        /// </summary>
        private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await Task.Run(() => sha256.ComputeHash(stream), cancellationToken);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Extract additional APK metadata using aapt/apkanalyzer
        /// </summary>
        private async Task<ApkMetadata> ExtractApkMetadataAsync(string path, CancellationToken cancellationToken)
        {
            // This would use aapt or apkanalyzer to extract detailed metadata
            // For now, return default values
            await Task.Delay(1, cancellationToken); // Placeholder for actual implementation
            
            return new ApkMetadata(
                SignerDigest: null,
                MinSdk: null,
                TargetSdk: null,
                RequiredSplits: null,
                RequiredFeatures: null
            );
        }

        /// <summary>
        /// Validate that all required splits are present
        /// </summary>
        private List<ApkFile> ValidateRequiredSplits(ApkFile baseApk, List<ApkFile> availableSplits)
        {
            var selectedSplits = new List<ApkFile>(availableSplits);
            
            // Check if base APK requires specific splits
            if (baseApk.RequiredSplits?.Any() == true)
            {
                var requiredSplits = baseApk.RequiredSplits ?? new List<string>();
                var missingSplits = requiredSplits.Where(required =>
                    !availableSplits.Any(s => string.Equals(s.SplitName, required, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                
                if (missingSplits.Any())
                {
                    // In strict mode, missing required splits means failure
                    throw new InvalidOperationException(
                        $"Missing required splits: {string.Join(", ", missingSplits)}");
                }
            }
            
            return selectedSplits;
        }

        /// <summary>
        /// Check if splits have conflicts (same profile, different content)
        /// </summary>
        private static bool HasSplitConflicts(List<ApkFile> splits)
        {
            var duplicateGroups = splits.GroupBy(s => s.GetProfileId())
                                      .Where(g => g.Select(x => x.Sha256).Distinct().Count() > 1);
            return duplicateGroups.Any();
        }

        /// <summary>
        /// Order files with base first, then splits by priority
        /// </summary>
        private static IReadOnlyList<ApkFile> OrderFiles(ApkFile baseApk, List<ApkFile> splits)
        {
            var orderedSplits = splits
                .OrderBy(f => string.IsNullOrEmpty(f.Abi) ? 1 : 0)      // ABI splits first
                .ThenBy(f => string.IsNullOrEmpty(f.Dpi) ? 1 : 0)       // DPI splits second
                .ThenBy(f => string.IsNullOrEmpty(f.Locale) ? 1 : 0)    // Locale splits third
                .ThenBy(f => f.SplitName, StringComparer.Ordinal)       // Others by name
                .ToList();
            
            return new[] { baseApk }.Concat(orderedSplits).ToList();
        }

        /// <summary>
        /// APK metadata extracted from manifest
        /// </summary>
        private record ApkMetadata(
            string? SignerDigest,
            int? MinSdk,
            int? TargetSdk,
            IReadOnlyList<string>? RequiredSplits,
            IReadOnlyList<string>? RequiredFeatures);
    }
}
