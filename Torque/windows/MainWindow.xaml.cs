using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Torque
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        internal MainWindowModel Model { get; } = new();
        TorqueService TorqueService { get; }
        IMesService MesService { get; }
        AppDbContext AppDbContext { get; }
        IServiceProvider sp;
        string? resultPath;
        StreamWriter? result;

        public MainWindow(TorqueService torqueService, IMesService mesService, AppDbContext appDbContext, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            DataContext = Model;
            TorqueService = torqueService;
            MesService = mesService;
            AppDbContext = appDbContext;
            sp = serviceProvider;
            TorqueService.OnData += HandleData;
            TorqueService.OnError += HandleError;
            Closed += (_, _) =>
            {
                TorqueService.OnData -= HandleData;
                TorqueService.OnError -= HandleError;
            };
            Directory.CreateDirectory("results");
        }

        private async void ResetTorque(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("要标定扭矩传感器零点并清除当前数据吗？", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                ZeroButton.IsEnabled = false;
                await TorqueService.Zero();
                Model.ClearTests();
                ZeroButton.IsEnabled = true;
            }
        }

        private void ReadTorque(object sender, RoutedEventArgs e)
        {
            StopButton.Visibility = Visibility.Visible;
            ZeroButton.IsEnabled = false;
            resultPath = Path.Combine("results", $"{DateTime.Now:yyyyMMddHHmmss}.csv");
            result = File.CreateText(resultPath);
            result.AutoFlush = true;
            result.WriteLine("milliseconds,torque");
            TorqueService.StartRead();
        }

        private void HandleData(long milliseconds, double torque)
        {
            result?.WriteLine($"{milliseconds},{torque}");
        }

        private void HandleError(Exception e)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(e.ToString(), e.Message, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private void AddTest(double torque)
        {
            var test = new Test
            {
                ToolId = Model.Tool.Id,
                SetTorque = Model.Tool.SetTorque,
                RealTorque = torque,
                Diviation = (torque - Model.Tool.SetTorque) / Model.Tool.SetTorque,
                TestTime = DateTime.Now
            };
            Dispatcher.InvokeAsync(() =>
            {
                Model.LastTest = test;
                Model.Tests.Add(test);
                if (!test.IsOK)
                {
                    if (MessageBox.Show(this, "数据NG，是否重新测量", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        Model.ClearTests();
                    }
                }
                else if (Model.Tests.Count >= 12 && Model.TestsAreOK)
                {
                    if (MessageBox.Show(this, "校准完成，是否上传数据", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        AppDbContext.Tests.AddRange(Model.Tests);
                        AppDbContext.SaveChanges();
                        MesService.Upload(Model.Tests);
                        Model.ClearTests();
                    }
                }
            });
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopButton.IsEnabled = false;
            await TorqueService.StopRead();
            StopButton.IsEnabled = true;
            StopButton.Visibility = Visibility.Hidden;
            ZeroButton.IsEnabled = true;
            result?.Dispose();
            var data = File.ReadAllLines(resultPath!)
                .Skip(1)
                .Select(r => double.Parse(r.Split(',')[1]));
            if (data.Any())
            {
                AddTest(data.Max());
            } else
            {
                MessageBox.Show("没有测量数据");
            }
        }

        private void ClearTests(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("要清除当前数据吗？", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Model.ClearTests();
            }
        }

        private void OpenTestsViewer(object sender, RoutedEventArgs e)
        {
            var windows = App.Current.Windows.OfType<Window>();
            if (!windows.Any(w => w is TestsViewer))
            {
                sp.GetRequiredService<TestsViewer>().Show();
            }
        }

        private void OpenUsersWindow(object sender, RoutedEventArgs e)
        {
            var windows = App.Current.Windows.OfType<Window>();
            if (!windows.Any(w => w is UsersWindow))
            {
                sp.GetRequiredService<UsersWindow>().Show();
            }
        }

        private void InputTool(object sender, RoutedEventArgs e)
        {
            var dialog = new InputToolDialog();
            if (dialog.ShowDialog() == true)
            {
                if (double.TryParse(dialog.setTorque.Text, out var setTorque))
                {
                    Model.Tool = new() { Id = dialog.id.Text, SetTorque = setTorque };
                    Model.ClearTests();
                }
                else
                {
                    MessageBox.Show("夹紧力无法解析成浮点数");
                }
            }
        }

        private void ScanTool(object sender, RoutedEventArgs e)
        {
            var dialog = new ScanDialog();
            if (dialog.ShowDialog() == true)
            {
                var tool = MesService.GetTool(dialog.id.Text);
                if (tool is null)
                {
                    MessageBox.Show(this, $"没找到电批{dialog.id.Text}");
                }
                else if (tool != Model.Tool)
                {
                    Model.Tool = tool;
                    Model.ClearTests();
                }
            }
        }

        private void UploadTests(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(this, "是否上传并清除当前数据", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                AppDbContext.Tests.AddRange(Model.Tests);
                AppDbContext.SaveChanges();
                MesService.Upload(Model.Tests);
                Model.ClearTests();
            }
        }
    }
}
