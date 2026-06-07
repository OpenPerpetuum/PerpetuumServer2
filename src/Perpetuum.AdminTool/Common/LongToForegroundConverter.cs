using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Perpetuum.AdminTool.Common
{
    public class LongToForegroundConverter : IValueConverter
    {
        public static readonly LongToForegroundConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is long v && v < 0 ? Brushes.DarkRed : Brushes.DarkGreen;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
