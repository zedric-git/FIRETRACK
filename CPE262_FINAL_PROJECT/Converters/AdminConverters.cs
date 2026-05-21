using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using System;
using Windows.UI;

namespace CPE262_FINAL_PROJECT.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
            => value is true
                ? Color.FromArgb(255, 76, 175, 80)
                : Color.FromArgb(255, 255, 82, 82);

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Color color)
            {
                return color == Color.FromArgb(255, 76, 175, 80);
            }

            return false;
        }
    }

    public class BoolToDisableTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
            => value is true ? "DISABLE" : "ENABLE";

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => value is string text && text.Equals("DISABLE", StringComparison.OrdinalIgnoreCase);
    }
}
