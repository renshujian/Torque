using System.Windows;

namespace Torque
{
    /// <summary>
    /// ScanDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ScanDialog : Window
    {
        public ScanDialog()
        {
            InitializeComponent();
            id.Focus();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
