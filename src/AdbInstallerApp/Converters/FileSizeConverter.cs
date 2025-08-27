using System;
using System.Globalization;
using System.Windows.Data;

namespace AdbInstallerApp.Converters
{
    /// <summary>
    /// Converts file size in bytes to human-readable format (KB, MB, GB)
    /// </summary>
    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "0 B";

            if (value is long longValue)
                return FormatFileSize(longValue);

            if (value is int intValue)
                return FormatFileSize(intValue);

            if (value is double doubleValue)
                return FormatFileSize((long)doubleValue);

            if (value is string stringValue && long.TryParse(stringValue, out long parsedValue))
                return FormatFileSize(parsedValue);

            return "0 B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("FileSizeConverter does not support ConvertBack");
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            // Format with appropriate precision
            if (order == 0) // Bytes
                return $"{len:0} {sizes[order]}";
            else if (order == 1) // KB
                return $"{len:0.##} {sizes[order]}";
            else if (order == 2) // MB
                return $"{len:0.##} {sizes[order]}";
            else // GB, TB
                return $"{len:0.##} {sizes[order]}";
        }
    }
}
