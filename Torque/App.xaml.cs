using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;

namespace Torque
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        void ShowException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.Message, e.Exception.GetType().FullName, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        void AppStartup(object sender, StartupEventArgs e)
        {
            var config = new ConfigurationBuilder().AddIniFile("config.ini").Build();
            var root = ConfigureServices(config);
            var scope = root.CreateScope();
            var sp = scope.ServiceProvider;
            sp.GetRequiredService<AppDbContext>().Database.Migrate();
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
            services.AddIdentityCore<User>().AddEntityFrameworkStores<AppDbContext>().AddTokenProvider<FakeTokenProvider>(TokenOptions.DefaultProvider);
            services.Configure<IdentityOptions>(o =>
            {
                o.User.AllowedUserNameCharacters = "";
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequireUppercase = false;
                o.Lockout.AllowedForNewUsers = false;
            });
            services.AddSingleton(config.GetSection(nameof(StaticTorqueServiceOptions)).Get<StaticTorqueServiceOptions>());
            services.AddSingleton<TorqueService, TorqueService>();
            services.AddSingleton<StaticTorqueService, StaticTorqueService>();
            var mesServiceOptions = config.GetSection(nameof(MesServiceOptions)).Get<MesServiceOptions>() ?? new();
            var mesDbContextOptionsBuilder = new DbContextOptionsBuilder<MesDbContext>().UseOracle(config.GetConnectionString("MES"), o => o.UseOracleSQLCompatibility("11"));
            if (mesServiceOptions.EnableSensitiveDataLogging)
            {
                var log = new StreamWriter("debug.log", append: true);
                mesDbContextOptionsBuilder.LogTo(s =>
                {
                    log.WriteLine(s);
                    log.Flush();
                }).EnableSensitiveDataLogging();
                using var con = new OracleConnection(config.GetConnectionString("MES"));
                con.Open();
                log.WriteLine($"oracle server version: {con.ServerVersion}");
                using var cmd = con.CreateCommand();
                cmd.CommandText = "select object_name, object_type, status from all_objects where upper(object_name) like '%SCREWDRIVER%'";
                using var reader = cmd.ExecuteReader();
                log.WriteLine("OBJECT_NAME, OBJECT_TYPE, STATUS");
                while (reader.Read())
                {
                    log.WriteLine($"{reader.GetString(0)}, {reader.GetString(1)}, {reader.GetString(2)}");
                }
            }
            services.AddSingleton(mesDbContextOptionsBuilder.Options);
            services.AddSingleton<MesDbContext>();
            services.AddSingleton<IMesService, MesService>();
            services.AddTransient<Login>().AddTransient<MainWindow>().AddTransient<TestsViewer>().AddTransient<UsersWindow>();
            return services.BuildServiceProvider();
        }
    }
}
