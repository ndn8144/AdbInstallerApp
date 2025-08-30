using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AdbInstallerApp.Services;

namespace AdbInstallerApp.Converters;

public class LogLevelToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Info => new SolidColorBrush(Colors.White),
                LogLevel.Warning => new SolidColorBrush(Color.FromRgb(255, 255, 200)), // Light yellow
                LogLevel.Error => new SolidColorBrush(Color.FromRgb(255, 200, 200)), // Light red
                LogLevel.Debug => new SolidColorBrush(Color.FromRgb(240, 240, 240)), // Light gray
                _ => new SolidColorBrush(Colors.White)
            };
        }
        
        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
