using System.Windows;

namespace Torque
{
    /// <summary>
    /// ToolInput.xaml 的交互逻辑
    /// </summary>
    public partial class InputToolDialog : Window
    {
        public InputToolDialog()
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
