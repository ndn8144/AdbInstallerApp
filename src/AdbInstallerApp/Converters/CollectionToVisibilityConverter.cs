using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AdbInstallerApp.Converters
{
    public class CollectionToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;

            bool hasItems = false;

            if (value is ICollection collection)
            {
                hasItems = collection.Count > 0;
            }
            else if (value is IEnumerable enumerable)
            {
                // Check if enumerable has any items
                var enumerator = enumerable.GetEnumerator();
                hasItems = enumerator.MoveNext();
            }

            // If parameter is "Invert", reverse the logic
            if (parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) == true)
            {
                hasItems = !hasItems;
            }

            return hasItems ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
