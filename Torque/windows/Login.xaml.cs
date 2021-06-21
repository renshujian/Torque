using System.Windows;
using Microsoft.AspNetCore.Identity;

namespace Torque
{
    /// <summary>
    /// Login.xaml 的交互逻辑
    /// </summary>
    public partial class Login : Window
    {
        UserManager<User> UserManager { get; }
        public User? User { get; set; }

        public Login(UserManager<User> userManager)
        {
            InitializeComponent();
            UserManager = userManager;
        }

        async void Submit(object sender, RoutedEventArgs e)
        {
            var user = await UserManager.FindByNameAsync(usernameBox.Text);
            var success = await UserManager.CheckPasswordAsync(user, passwordBox.Password);
            if (success)
            {
                User = user;
                DialogResult = true;
            } else
            {
                MessageBox.Show(this, "用户名或密码错误", "登录失败");
                passwordBox.Password = "";
            }
        }
    }
}
