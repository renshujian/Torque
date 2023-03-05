using System.Windows;

namespace Torque
{
    public partial class AddSamplingDialog : Window
    {
        public AddSamplingDialog()
        {
            InitializeComponent();
            time.Focus();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
