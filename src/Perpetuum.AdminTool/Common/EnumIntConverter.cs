using System;
using System.Globalization;
using System.Windows.Data;

namespace Perpetuum.AdminTool.Common
{
    public class EnumIntConverter : IValueConverter
    {
        public static readonly EnumIntConverter Instance = new EnumIntConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "";
            if (value is Enum) return System.Convert.ToInt32(value).ToString(CultureInfo.InvariantCulture);
            return value.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
