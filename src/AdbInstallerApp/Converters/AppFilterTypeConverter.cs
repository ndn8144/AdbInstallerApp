using System;
using System.Globalization;
using System.Windows.Data;
using AdbInstallerApp.ViewModels;

namespace AdbInstallerApp.Converters
{
    /// <summary>
    /// Converter for AppFilterType enum to display friendly names
    /// </summary>
    public class AppFilterTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AppFilterType filterType)
            {
                return filterType switch
                {
                    AppFilterType.All => "All Apps",
                    AppFilterType.UserApps => "User Apps Only",
                    AppFilterType.SystemApps => "System Apps Only",
                    _ => value.ToString() ?? string.Empty
                };
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                return stringValue switch
                {
                    "All Apps" => AppFilterType.All,
                    "User Apps" => AppFilterType.UserApps,
                    "System Apps" => AppFilterType.SystemApps,
                    _ => AppFilterType.All
                };
            }
            return AppFilterType.All;
        }
    }
}
