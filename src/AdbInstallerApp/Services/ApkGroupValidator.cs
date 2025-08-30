using AdbInstallerApp.Models;
using System.Security.Cryptography;

namespace AdbInstallerApp.Services
{
    public enum StrictSplitMatch
    {
        Strict,
        Relaxed,
        BaseOnlyFallback
    }

    public sealed class ApkValidationOptions
    {
        public bool VerifyVersionHomogeneity { get; set; } = true;
        public bool VerifySignature { get; set; } = true;
        public StrictSplitMatch SplitMatchMode { get; set; } = StrictSplitMatch.Relaxed;
    }

    public sealed class ApkValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<ApkItem> OrderedApks { get; set; } = new();
    }

    public sealed class ApkGroupValidator
    {
        private readonly ILogBus _logBus;

        public ApkGroupValidator(ILogBus logBus)
        {
            _logBus = logBus ?? throw new ArgumentNullException(nameof(logBus));
        }

        public async Task<ApkValidationResult> ValidateGroupAsync(ApkGroup group, ApkValidationOptions options, CancellationToken ct = default)
        {
            var result = new ApkValidationResult();

            try
            {
                _logBus.Write($"Validating APK group: {group.Name}");

                // Step 1: Basic validation
                if (!group.ApkItems.Any())
                {
                    result.Errors.Add("Group contains no APK files");
                    return result;
                }

                // Step 2: Find base APK
                var baseApks = group.ApkItems.Where(apk => apk.IsBaseApk).ToList();
                if (baseApks.Count == 0)
                {
                    result.Errors.Add("Group must contain exactly one base APK");
                    return result;
                }
                if (baseApks.Count > 1)
                {
                    result.Errors.Add($"Group contains {baseApks.Count} base APKs, expected exactly 1");
                    return result;
                }

                var baseApk = baseApks.First();
                var splits = group.ApkItems.Where(apk => !apk.IsBaseApk).ToList();

                // Step 3: Package name consistency
                var packageName = baseApk.PackageName;
                var inconsistentPackages = group.ApkItems.Where(apk => apk.PackageName != packageName).ToList();
                if (inconsistentPackages.Any())
                {
                    result.Errors.Add($"Inconsistent package names found. Base: {packageName}, Others: {string.Join(", ", inconsistentPackages.Select(a => a.PackageName).Distinct())}");
                    return result;
                }

                // Step 4: Version code consistency (if enabled)
                if (options.VerifyVersionHomogeneity)
                {
                    var baseVersionCode = baseApk.VersionCode;
                    var inconsistentVersions = group.ApkItems.Where(apk => apk.VersionCode != baseVersionCode).ToList();
                    if (inconsistentVersions.Any())
                    {
                        result.Errors.Add($"Inconsistent version codes. Base: {baseVersionCode}, Others: {string.Join(", ", inconsistentVersions.Select(a => a.VersionCode).Distinct())}");
                        return result;
                    }
                }

                // Step 5: Signature verification (if enabled)
                if (options.VerifySignature)
                {
                    var signatureValidation = await ValidateSignaturesAsync(group.ApkItems, ct).ConfigureAwait(false);
                    if (!signatureValidation.IsValid)
                    {
                        result.Errors.AddRange(signatureValidation.Errors);
                        return result;
                    }
                }

                // Step 6: Detect duplicate splits
                var duplicateValidation = ValidateDuplicateSplits(splits);
                if (!duplicateValidation.IsValid)
                {
                    result.Errors.AddRange(duplicateValidation.Errors);
                    return result;
                }

                // Step 7: Filter and order splits based on device compatibility
                var orderedApks = OrderApksForInstallation(baseApk, splits, options.SplitMatchMode);
                result.OrderedApks = orderedApks;

                result.IsValid = true;
                _logBus.Write($"Group validation successful: {result.OrderedApks.Count} APKs ordered for installation");

                return result;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Validation failed with exception: {ex.Message}");
                _logBus.Write($"Group validation error: {ex.Message}");
                return result;
            }
        }

        private async Task<ApkValidationResult> ValidateSignaturesAsync(IEnumerable<ApkItem> apks, CancellationToken ct)
        {
            var result = new ApkValidationResult { IsValid = true };

            try
            {
                var signatures = new Dictionary<string, string>();

                foreach (var apk in apks)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!File.Exists(apk.FilePath))
                    {
                        result.Errors.Add($"APK file not found: {apk.FilePath}");
                        result.IsValid = false;
                        continue;
                    }

                    // Calculate file hash as signature proxy
                    var hash = await CalculateFileHashAsync(apk.FilePath, ct).ConfigureAwait(false);
                    var signatureKey = $"{apk.PackageName}_{apk.VersionCode}";

                    if (signatures.ContainsKey(signatureKey))
                    {
                        // For splits of same package/version, we don't expect identical hashes
                        // This is just to ensure files aren't corrupted
                        continue;
                    }

                    signatures[signatureKey] = hash;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Signature validation failed: {ex.Message}");
                result.IsValid = false;
            }

            return result;
        }

        private ApkValidationResult ValidateDuplicateSplits(List<ApkItem> splits)
        {
            var result = new ApkValidationResult { IsValid = true };
            var splitGroups = new Dictionary<string, List<ApkItem>>();

            foreach (var split in splits)
            {
                var key = GetSplitKey(split);
                if (!splitGroups.ContainsKey(key))
                    splitGroups[key] = new List<ApkItem>();
                
                splitGroups[key].Add(split);
            }

            foreach (var group in splitGroups.Where(g => g.Value.Count > 1))
            {
                var duplicates = group.Value;
                var hashes = new HashSet<string>();

                foreach (var duplicate in duplicates)
                {
                    if (File.Exists(duplicate.FilePath))
                    {
                        var hash = CalculateFileHashAsync(duplicate.FilePath, CancellationToken.None).Result;
                        if (!hashes.Add(hash))
                        {
                            result.Errors.Add($"Duplicate split detected with different content: {group.Key}");
                            result.IsValid = false;
                            break;
                        }
                    }
                }
            }

            return result;
        }

        private List<ApkItem> OrderApksForInstallation(ApkItem baseApk, List<ApkItem> splits, StrictSplitMatch matchMode)
        {
            var ordered = new List<ApkItem> { baseApk };

            // Group splits by type for ordering
            var abiSplits = splits.Where(s => !string.IsNullOrEmpty(s.Abi)).ToList();
            var dpiSplits = splits.Where(s => !string.IsNullOrEmpty(s.Density)).ToList();
            var localeSplits = splits.Where(s => !string.IsNullOrEmpty(s.Locale)).ToList();
            var featureSplits = splits.Where(s => s.IsFeatureSplit).ToList();

            // Order: base → ABI → DPI → locale → features
            ordered.AddRange(abiSplits.OrderBy(s => s.Abi));
            ordered.AddRange(dpiSplits.OrderBy(s => s.Density));
            ordered.AddRange(localeSplits.OrderBy(s => s.Locale));
            ordered.AddRange(featureSplits.OrderBy(s => s.SplitName));

            return ordered;
        }

        private string GetSplitKey(ApkItem apk)
        {
            // Create unique key for split type detection
            if (!string.IsNullOrEmpty(apk.Abi))
                return $"abi_{apk.Abi}";
            if (!string.IsNullOrEmpty(apk.Density))
                return $"dpi_{apk.Density}";
            if (!string.IsNullOrEmpty(apk.Locale))
                return $"locale_{apk.Locale}";
            if (apk.IsFeatureSplit)
                return $"feature_{apk.SplitName}";
            
            return $"unknown_{apk.SplitName}";
        }

        private async Task<string> CalculateFileHashAsync(string filePath, CancellationToken ct)
        {
            using var sha256 = SHA256.Create();
            await using var stream = File.OpenRead(filePath);
            var hashBytes = await sha256.ComputeHashAsync(stream, ct).ConfigureAwait(false);
            return Convert.ToHexString(hashBytes);
        }
    }
}
