﻿using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

        async void AppStartup(object sender, StartupEventArgs e)
        {
            var root = ConfigureServices();
#if DEBUG
            using (var s = root.CreateScope())
            {
                await SeedDb(s.ServiceProvider);
            }
#endif
            using var scope = root.CreateScope();
            var sp = scope.ServiceProvider;
            var login = sp.GetRequiredService<Login>();
            var main = sp.GetRequiredService<MainWindow>();
            if (login.ShowDialog() == true)
            {
                MainWindow = main;
                MainWindow.Show();
            } else
            {
                Shutdown();
            }
        }
 
        static ServiceProvider ConfigureServices()
        {
            ServiceCollection services = new();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite("DataSource=app.db;Cache=Shared"));
            services.AddIdentityCore<User>().AddEntityFrameworkStores<AppDbContext>();
            services.Configure<IdentityOptions>(o =>
            {
                o.User.AllowedUserNameCharacters = "";
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequireUppercase = false;
                o.Lockout.AllowedForNewUsers = false;
            });
            services.AddTransient<Login>().AddTransient<MainWindow>();
            return services.BuildServiceProvider();
        }

        static async Task SeedDb(IServiceProvider sp)
        {
            var db = sp.GetRequiredService<AppDbContext>();
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
            var userManager = sp.GetRequiredService<UserManager<User>>();
            await userManager.CreateAsync(new("查看"), "chakan");
            await userManager.CreateAsync(new("操作员"), "caozuoyuan");
            await userManager.CreateAsync(new("管理员"), "guanliyuan");
        }
    }
}