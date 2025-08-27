using AdbInstallerApp.Models;
using CommunityToolkit.Mvvm.ComponentModel; // cspell:disable-line
using System.ComponentModel;
using System.IO;

namespace AdbInstallerApp.ViewModels
{
    public partial class ApkItemViewModel : ObservableObject
    {
        public ApkItem Model { get; }

        public ApkItemViewModel(ApkItem m)
        {
            Model = m ?? throw new ArgumentNullException(nameof(m));

            // Subscribe to property changes to notify parent ViewModel
            PropertyChanged += OnPropertyChanged;
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Remove this method to avoid potential circular references
            // The ObservableObject base class already handles property change notifications
        }

        public string FileName => Model?.FileName ?? "Unknown";

        public string FilePath => Model?.FilePath ?? "Unknown";

        public string FileSize
        {
            get
            {
                if (Model.FileSize > 0)
                {
                    return FormatFileSize(Model.FileSize);
                }
                return "Unknown";
            }
        }

        public string LastModified
        {
            get
            {
                if (Model?.LastModified != DateTime.MinValue)
                {
                    return Model!.LastModified.ToString("yyyy-MM-dd HH:mm");
                }
                return "Unknown";
            }
        }

        public string SplitTag
        {
            get
            {
                if (string.IsNullOrEmpty(Model?.SplitTag))
                    return "📱 Base";

                // Enhanced split tag display with icons and better formatting
                return Model.SplitTag switch
                {
                    "Base" => "📱 Base",
                    "ARM64" => "🖥️ ARM64",
                    "ARM" => "🖥️ ARM",
                    "X86" => "🖥️ X86",
                    "X86_64" => "🖥️ X86_64",
                    "XXHDPI" => "📱 XXHDPI",
                    "XHDPI" => "📱 XHDPI",
                    "HDPI" => "📱 HDPI",
                    "MDPI" => "📱 MDPI",
                    "LDPI" => "📱 LDPI",
                    "NODPI" => "📱 NODPI",
                    "TVDPI" => "📱 TVDPI",
                    "XXXHDPI" => "📱 XXXHDPI",
                    _ => Model.SplitTag.StartsWith("SPLIT_") ? $"📦 {Model.SplitTag}" : $"📦 {Model.SplitTag}"
                };
            }
        }

        public string DisplayInfo
        {
            get
            {
                if (Model == null) return "Unknown APK";

                // Enhanced display with package name and version
                var displayParts = new List<string>();

                if (!string.IsNullOrEmpty(Model.Package))
                {
                    displayParts.Add(Model.Package.Trim());
                }
                else if (!string.IsNullOrEmpty(Model.FileName))
                {
                    // Fallback to filename if no package name
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(Model.FileName);
                    displayParts.Add(fileNameWithoutExt);
                }

                // Add version if available
                if (!string.IsNullOrEmpty(Model.Version))
                {
                    displayParts.Add($"v{Model.Version}");
                }

                // Add SDK info if available
                if (!string.IsNullOrEmpty(Model.TargetSdk))
                {
                    displayParts.Add($"API {Model.TargetSdk}");
                }

                if (displayParts.Count > 0)
                {
                    return string.Join(" - ", displayParts);
                }

                return "Unknown APK";
            }
        }

        public string ApkType
        {
            get
            {
                if (string.IsNullOrEmpty(Model?.SplitTag) || Model.SplitTag == "Base")
                    return "📱 Base APK";

                return $"📦 Split APK ({Model.SplitTag})";
            }
        }

        public string CompatibilityInfo
        {
            get
            {
                var info = new List<string>();

                if (!string.IsNullOrEmpty(Model.TargetSdk))
                {
                    info.Add($"Target: API {Model.TargetSdk}");
                }

                if (!string.IsNullOrEmpty(Model.MinSdk))
                {
                    info.Add($"Min: API {Model.MinSdk}");
                }

                if (!string.IsNullOrEmpty(Model.SplitTag) && Model.SplitTag != "Base")
                {
                    info.Add($"Arch: {Model.SplitTag}");
                }

                return info.Count > 0 ? string.Join(", ", info) : "Standard";
            }
        }

        public string ClassificationSummary
        {
            get
            {
                if (string.IsNullOrEmpty(Model?.SplitTag) || Model.SplitTag == "Base")
                {
                    return $"📱 Base APK - {Model?.Package ?? "Unknown Package"}";
                }

                var package = Model?.Package ?? "Unknown Package";
                var splitTag = Model?.SplitTag ?? "Unknown";
                return $"📦 Split APK - {package} ({splitTag})";
            }
        }

        private static string FormatFileSize(long bytes)
        {
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

        public override string ToString()
        {
            return DisplayInfo;
        }
    }
}