using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AdbInstallerApp.Converters
{
    public class EnumToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            try
            {
                // Parse the parameter as the target enum value
                Type enumType = value.GetType();
                if (enumType.IsEnum)
                {
                    string? paramString = parameter.ToString();
                    if (paramString != null)
                    {
                        object targetEnum = Enum.Parse(enumType, paramString);
                        return value.Equals(targetEnum) ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
            catch
            {
                // If parsing fails, return collapsed
                return Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
