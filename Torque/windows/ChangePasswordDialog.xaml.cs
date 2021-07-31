using System.Windows;

namespace Torque
{
    /// <summary>
    /// ChangePasswordDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ChangePasswordDialog : Window
    {
        public ChangePasswordDialog()
        {
            InitializeComponent();
            passwordBox.Focus();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (passwordBox.Password != passwordBox2.Password)
            {
                MessageBox.Show("两次输入密码不一致");
            }
            else
            {
                DialogResult = true;
            }
        }
    }
}
