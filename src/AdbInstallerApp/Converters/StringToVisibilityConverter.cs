using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AdbInstallerApp.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;

            string? stringValue = value.ToString();

            // If parameter is provided, check if string equals parameter
            if (parameter != null)
            {
                string? targetString = parameter.ToString();
                if (stringValue != null && targetString != null)
                {
                    return string.Equals(stringValue, targetString, StringComparison.OrdinalIgnoreCase)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }

            // If no parameter, show if string is not empty
            return string.IsNullOrWhiteSpace(stringValue) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
