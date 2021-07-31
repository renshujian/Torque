using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace Torque
{
    class IndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var item = (ListViewItem)value;
            var index = ItemsControl.ItemsControlFromItemContainer(item).ItemContainerGenerator.IndexFromContainer(item);
            return index + 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
