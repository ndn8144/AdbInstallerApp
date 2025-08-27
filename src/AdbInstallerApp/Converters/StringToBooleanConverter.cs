using System;
using System.Globalization;
using System.Windows.Data;

namespace AdbInstallerApp.Converters
{
    public class StringToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                // If parameter is provided, check specific string values
                if (parameter is string format)
                {
                    var parts = format.Split('|');
                    if (parts.Length >= 2)
                    {
                        return stringValue.Equals(parts[0], StringComparison.OrdinalIgnoreCase);
                    }
                }

                // Default behavior: return true if string is not null/empty/whitespace
                return !string.IsNullOrWhiteSpace(stringValue);
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (parameter is string format)
                {
                    var parts = format.Split('|');
                    if (parts.Length >= 2)
                    {
                        return boolValue ? parts[0] : parts[1];
                    }
                }

                // Default values
                return boolValue ? "true" : "false";
            }

            return "false";
        }
    }
}
