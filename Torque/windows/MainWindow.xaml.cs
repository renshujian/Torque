using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
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
        StringBuilder scaned = new();
        Timer getToolDelayed;
        IServiceProvider sp;

        public MainWindow(TorqueService torqueService, IMesService mesService, AppDbContext appDbContext, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            DataContext = Model;
            TorqueService = torqueService;
            MesService = mesService;
            AppDbContext = appDbContext;
            sp = serviceProvider;
            getToolDelayed = new(_ =>
            {
                var id = scaned.ToString().TrimEnd();
                scaned.Clear();
                var tool = MesService.GetTool(id);
                if (tool is null)
                {
                    Dispatcher.Invoke(() => MessageBox.Show(this, $"没找到电批{id}"));
                }
                else if (tool != Model.Tool)
                {
                    Model.Tool = tool;
                    Dispatcher.Invoke(Model.ClearTests);
                }
            });
            TorqueService.StopRecording += AddTest;
            Closed += (_, _) => TorqueService.StopRecording -= AddTest;
        }

        private void Window_TextInput(object sender, TextCompositionEventArgs e)
        {
            scaned.Append(e.Text);
            getToolDelayed.Change(300, Timeout.Infinite);
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
            TorqueService.Threshold = TorqueService.Options.Threshold * Model.Tool!.SetTorque;
            TorqueService.StartRead();
        }

        private void AddTest(double[] data)
        {
            if (Model.SaveTorqueData)
            {
                File.WriteAllText($"torque-{DateTime.Now:yyyyMMddHHmmss}.txt", string.Join("\r\n", data));
            }
            Array.Sort(data, (x, y) => y.CompareTo(x));
            var torque = data.Take(TorqueService.Options.Sample).Average();
            if (torque > 2 * Model.Tool!.SetTorque) return; // 丢弃扭矩测量操作失误引发的无效结果
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
                else if (Model.Tests.Count >= 12 && Model.Tests.All(t => t.IsOK))
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

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopButton.Visibility = Visibility.Hidden;
            ZeroButton.IsEnabled = true;
            TorqueService.StopRead();
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
                    MessageBox.Show("扭矩无法解析成浮点数");
                }
            }
        }
    }
}
