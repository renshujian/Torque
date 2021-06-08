using System;
using System.Globalization;
using System.Net;
using System.Windows.Data;

namespace Torque
{
    [ValueConversion(typeof(IPAddress), typeof(string))]
    public class IpStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.ToString() ?? throw new NullReferenceException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return IPAddress.TryParse((string)value, out var ip) ? ip : IPAddress.Loopback;
        }
    }
}
