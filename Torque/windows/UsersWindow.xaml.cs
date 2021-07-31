using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.AspNetCore.Identity;

namespace Torque
{
    /// <summary>
    /// UsersWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UsersWindow : Window
    {
        readonly UserManager<User> manager;

        public UsersWindow(UserManager<User> userManager)
        {
            InitializeComponent();
            manager = userManager;
        }

        async Task ChangePassword(string userName)
        {
            var dialog = new ChangePasswordDialog();
            if (dialog.ShowDialog() == true)
            {
                var user = await manager.FindByNameAsync(userName);
                var result = await manager.ResetPasswordAsync(user, "", dialog.passwordBox.Password);
                if (!result.Succeeded)
                {
                    var errors = string.Join("\r\n", result.Errors.Select(e => e.Description));
                    MessageBox.Show(errors, "修改密码失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void Change查看Password(object sender, RoutedEventArgs e)
        {
            await ChangePassword("查看");
        }

        private async void Change操作员Password(object sender, RoutedEventArgs e)
        {
            await ChangePassword("操作员");
        }

        private async void Change管理员Password(object sender, RoutedEventArgs e)
        {
            await ChangePassword("管理员");
        }
    }
}
