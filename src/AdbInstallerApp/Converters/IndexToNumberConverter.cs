using System;
using System.Globalization;
using System.Windows.Data;

namespace AdbInstallerApp.Converters
{
    /// <summary>
    /// Converter để chuyển đổi index thành số thứ tự (bắt đầu từ 1)
    /// </summary>
    public class IndexToNumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return (index + 1).ToString();
            }
            return "1";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
