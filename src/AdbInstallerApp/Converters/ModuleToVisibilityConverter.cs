using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AdbInstallerApp.Converters
{
    public class ModuleToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string currentModule && parameter is string targetModule)
            {
                return currentModule == targetModule ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
