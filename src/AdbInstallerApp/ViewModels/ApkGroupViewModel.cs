using AdbInstallerApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace AdbInstallerApp.ViewModels
{
    public partial class ApkGroupViewModel : ObservableObject
    {
        public ApkGroup Model { get; }
        
        public ObservableCollection<ApkItemViewModel> ApkItems { get; } = new();
        
        public ApkGroupViewModel(ApkGroup model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            
            // Subscribe to model's ApkItems changes
            Model.ApkItems.CollectionChanged += OnModelApkItemsChanged;
            
            // Initialize APK ViewModels
            RefreshApkItems();
        }
        
        private void OnModelApkItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                RefreshApkItems();
                OnPropertyChanged(nameof(ApkCount));
                OnPropertyChanged(nameof(TotalSizeText));
                OnPropertyChanged(nameof(GroupInfo));
            });
        }
        
        private void RefreshApkItems()
        {
            try
            {
                // Clear current ViewModels
                foreach (var vm in ApkItems)
                {
                    vm.PropertyChanged -= OnApkItemPropertyChanged;
                }
                ApkItems.Clear();
                
                // Add new ViewModels
                foreach (var apk in Model.ApkItems)
                {
                    var vm = new ApkItemViewModel(apk);
                    vm.PropertyChanged += OnApkItemPropertyChanged;
                    ApkItems.Add(vm);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing APK items: {ex.Message}");
            }
        }
        
        private void OnApkItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ApkItemViewModel.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedApkCount));
                OnPropertyChanged(nameof(IsAnyApkSelected));
                OnPropertyChanged(nameof(AreAllApksSelected));
            }
        }

        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        private bool _isSelected;
        
        [RelayCommand]
        private void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
        }

        public string Id => Model.Id;
        
        public string Name
        {
            get => Model.Name;
            set
            {
                if (Model.Name != value)
                {
                    Model.Name = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }
        
        public string Description
        {
            get => Model.Description;
            set
            {
                if (Model.Description != value)
                {
                    Model.Description = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public string Color
        {
            get => Model.Color;
            set
            {
                if (Model.Color != value)
                {
                    Model.Color = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public DateTime CreatedAt => Model.CreatedAt;
        
        public int ApkCount => Model.ApkCount;
        
        public long TotalSize => Model.TotalSize;
        
        public string TotalSizeText => FormatFileSize(TotalSize);
        
        public string DisplayName => Model.DisplayName;
        
        public string GroupInfo => Model.GroupInfo;
        
        public int SelectedApkCount => ApkItems.Count(a => a.IsSelected);
        
        public bool IsAnyApkSelected => ApkItems.Any(a => a.IsSelected);
        
        public bool AreAllApksSelected => ApkItems.All(a => a.IsSelected);
        
        public string CreatedAtText => Model.CreatedAtText;
        
        // Commands will be handled by parent ViewModel (MainViewModel)
        
        public void AddApk(ApkItemViewModel apkViewModel)
        {
            if (apkViewModel?.Model != null)
            {
                Model.AddApk(apkViewModel.Model);
            }
        }
        
        public void RemoveApk(ApkItemViewModel apkViewModel)
        {
            if (apkViewModel?.Model != null)
            {
                Model.RemoveApk(apkViewModel.Model);
            }
        }
        
        public void SelectAllApks()
        {
            foreach (var apk in ApkItems)
            {
                apk.IsSelected = true;
            }
        }
        
        public void ClearApkSelection()
        {
            foreach (var apk in ApkItems)
            {
                apk.IsSelected = false;
            }
        }

        public void Dispose()
        {
            try
            {
                // Unsubscribe from events
                foreach (var vm in ApkItems)
                {
                    vm.PropertyChanged -= OnApkItemPropertyChanged;
                }
                
                if (Model.ApkItems != null)
                {
                    Model.ApkItems.CollectionChanged -= OnModelApkItemsChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing ApkGroupViewModel: {ex.Message}");
            }
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
        
        public override string ToString()
        {
            return DisplayName;
        }
    }
}