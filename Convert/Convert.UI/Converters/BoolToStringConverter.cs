using System.Globalization;
using System.Windows.Data;

namespace Convert.UI.Converters
{
    public class BoolToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var parts = parameter?.ToString()?.Split('|');
            if (parts == null || parts.Length != 2)
                return "";

            return (bool)value ? parts[0] : parts[1];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

}
