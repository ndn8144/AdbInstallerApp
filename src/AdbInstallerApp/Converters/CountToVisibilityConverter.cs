using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AdbInstallerApp.Converters
{
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int count = 0;

            // Handle different types that can represent count
            if (value is ICollection collection)
            {
                count = collection.Count;
            }
            else if (value is int intValue)
            {
                count = intValue;
            }
            else if (value is long longValue)
            {
                count = (int)longValue;
            }
            else if (value is string stringValue && int.TryParse(stringValue, out int parsedValue))
            {
                count = parsedValue;
            }

            // Parse parameter for threshold comparison
            if (parameter is string paramStr)
            {
                if (paramStr.Contains(">"))
                {
                    var parts = paramStr.Split('>');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int threshold))
                    {
                        return count > threshold ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
                else if (paramStr.Contains(">="))
                {
                    var parts = paramStr.Split(new[] { ">=" }, StringSplitOptions.None);
                    if (parts.Length == 2 && int.TryParse(parts[1], out int threshold))
                    {
                        return count >= threshold ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
                else if (paramStr.Contains("<"))
                {
                    var parts = paramStr.Split('<');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int threshold))
                    {
                        return count < threshold ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
                else if (paramStr.Contains("<="))
                {
                    var parts = paramStr.Split(new[] { "<=" }, StringSplitOptions.None);
                    if (parts.Length == 2 && int.TryParse(parts[1], out int threshold))
                    {
                        return count <= threshold ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
                else if (paramStr.Contains("=="))
                {
                    var parts = paramStr.Split(new[] { "==" }, StringSplitOptions.None);
                    if (parts.Length == 2 && int.TryParse(parts[1], out int threshold))
                    {
                        return count == threshold ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
                else if (paramStr.Contains("!="))
                {
                    var parts = paramStr.Split(new[] { "!=" }, StringSplitOptions.None);
                    if (parts.Length == 2 && int.TryParse(parts[1], out int threshold))
                    {
                        return count != threshold ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
                else if (int.TryParse(paramStr, out int simpleThreshold))
                {
                    // Simple threshold - show if count >= threshold
                    return count >= simpleThreshold ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            // Default behavior - show if count > 0
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
