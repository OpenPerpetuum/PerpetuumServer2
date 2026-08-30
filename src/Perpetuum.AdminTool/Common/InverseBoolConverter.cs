using System;
using System.Globalization;
using System.Windows.Data;

namespace Perpetuum.AdminTool.Common
{
    public class InverseBoolConverter : IValueConverter
    {
        public static readonly InverseBoolConverter Instance = new InverseBoolConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : true;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : false;
    }
}
