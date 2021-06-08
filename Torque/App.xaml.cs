using System.Windows;
using System.Windows.Threading;

namespace Torque
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        void ShowException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.Message, "发生异常", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
