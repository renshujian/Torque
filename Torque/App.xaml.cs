using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Torque
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        void ShowException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.Message, e.Exception.ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
        }

        void AppStartup(object sender, StartupEventArgs e)
        {
            var config = new ConfigurationBuilder().AddIniFile("config.ini").Build();
            var root = ConfigureServices(config);
            var scope = root.CreateScope();
            var sp = scope.ServiceProvider;
            var login = sp.GetRequiredService<Login>();
            var main = sp.GetRequiredService<MainWindow>();
            if (login.ShowDialog() == true)
            {
                if (login.User!.IsInRole("查看"))
                {
                    MainWindow = sp.GetRequiredService<TestsViewer>();
                    MainWindow.Show();
                    // App默认在关闭所有窗口时退出，所以关闭Login前需要先实例化MainWindow以防退出，这里又要关闭main以防不退出
                    main.Close();
                }
                else
                {
                    main.Model.User = login.User;
                    MainWindow = main;
                    MainWindow.Show();
                }
            } else
            {
                Shutdown();
            }
        }
 
        static ServiceProvider ConfigureServices(IConfiguration config)
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
            services.AddSingleton(config.GetSection(nameof(TorqueServiceOptions)).Get<TorqueServiceOptions>());
            services.AddSingleton<ITorqueService, TorqueService>();
            services.AddDbContext<MesDbContext>(o => o.UseOracle(config.GetConnectionString("MES"), o => o.UseOracleSQLCompatibility("11")));
            services.AddScoped<IMesService, MesService>();
            services.AddTransient<Login>().AddTransient<MainWindow>().AddTransient<TestsViewer>();
            return services.BuildServiceProvider();
        }
    }
}
