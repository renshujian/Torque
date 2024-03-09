using System;
using System.Globalization;
using System.Windows.Data;

namespace Torque
{
    class TickStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is double tick ? TimeSpan.FromTicks((long)tick).ToString() : null;
        }

        public object? ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && TimeSpan.TryParse(s, out var timeSpan))
            {
                return (double)timeSpan.Ticks;
            }
            else
            {
                return null;
            }
        }
    }
}
