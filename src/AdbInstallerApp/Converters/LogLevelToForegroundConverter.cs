using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AdbInstallerApp.Services;

namespace AdbInstallerApp.Converters;

public class LogLevelToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Info => new SolidColorBrush(Colors.Black),
                LogLevel.Warning => new SolidColorBrush(Colors.DarkOrange),
                LogLevel.Error => new SolidColorBrush(Colors.DarkRed),
                LogLevel.Debug => new SolidColorBrush(Colors.DarkGray),
                _ => new SolidColorBrush(Colors.Black)
            };
        }
        
        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
