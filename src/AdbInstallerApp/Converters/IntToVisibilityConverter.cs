using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AdbInstallerApp.Converters
{
    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                if (parameter is string paramStr && int.TryParse(paramStr, out int minCount))
                {
                    return intValue >= minCount ? Visibility.Visible : Visibility.Collapsed;
                }
                return intValue > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            
            if (value is long longValue)
            {
                if (parameter is string paramStr && int.TryParse(paramStr, out int minCount))
                {
                    return longValue >= minCount ? Visibility.Visible : Visibility.Collapsed;
                }
                return longValue > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
