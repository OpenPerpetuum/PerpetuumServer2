using System;
using System.Globalization;
using System.Windows.Data;
using Perpetuum.AdminTool.Entities;

namespace Perpetuum.AdminTool.Common
{
    public class CategoryFlagsDescriptionConverter : IValueConverter
    {
        public static readonly CategoryFlagsDescriptionConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long l) return CategoryFlagsCatalog.Describe(l);
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    public class AttributeFlagsDescriptionConverter : IValueConverter
    {
        public static readonly AttributeFlagsDescriptionConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long l) return AttributeFlagsCatalog.Describe(unchecked((ulong)l));
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
