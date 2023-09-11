using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Torque
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        internal MainWindowModel Model { get; } = new();
        StaticTorqueService TorqueService { get; }
        IMesService MesService { get; }
        AppDbContext AppDbContext { get; }
        IServiceProvider sp;

        public MainWindow(StaticTorqueService torqueService, IMesService mesService, AppDbContext appDbContext, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            DataContext = Model;
            TorqueService = torqueService;
            MesService = mesService;
            AppDbContext = appDbContext;
            sp = serviceProvider;
            TorqueService.StopRecording += AddTest;
            TorqueService.OnError += HandleError;
            TorqueService.OnSocketException += HandleSocketException;
            Closed += (_, _) =>
            {
                TorqueService.StopRecording -= AddTest;
                TorqueService.OnError -= HandleError;
                torqueService.OnSocketException -= HandleSocketException;
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
            TorqueService.BeginThreshold = TorqueService.Options.BeginThreshold * Model.Tool!.SetTorque;
            TorqueService.EndThreshold = TorqueService.Options.EndThreshold * Model.Tool!.SetTorque;
            TorqueService.StartRead();
        }

        private void HandleError(Exception e)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(e.ToString(), e.Message, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private bool HandleSocketException(Exception e)
        {
            return Dispatcher.Invoke(() =>
            {
                if (MessageBox.Show(e.ToString(), "传感器连接异常，是否重连？", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    return true;
                }
                else
                {
                    StopButton_Click(null!, null!);
                    return false;
                }
            });
        }

        private void AddTest(double[] data)
        {
            // 只采集到1个点可以认为是噪音不做处理，简化后续判断
            if (data.Length <= 1) return;

            string timestamp = $"{DateTime.Now:yyyyMMddHHmmss}";
            File.WriteAllTextAsync(Path.Combine("results", $"{timestamp}.txt"), string.Join("\r\n", data));
            Dispatcher.InvokeAsync(() =>
            {
                List<double> peaks = new();
                var rising = true;
                for (int i = 1; i < data.Length; i++)
                {
                    if (data[i] > data[i - 1])
                    {
                        rising = true;
                    }
                    else if (data[i] < data[i - 1] && rising)
                    {
                        rising = false;
                        peaks.Add(data[i - 1]);
                    }
                }
                if (rising)
                {
                    // rising = false;
                    peaks.Add(data[^1]);
                }
                File.WriteAllTextAsync(Path.Combine("results", $"{timestamp}-peaks.txt"), string.Join("\r\n", peaks));

                var torque = Model.PeakIndex switch
                {
                    "最大值" => peaks.Max(),
                    "第二大" => peaks.OrderByDescending(it => it).ElementAtOrDefault(1),
                    "第一个" => peaks[0],
                    "第二个" => peaks.ElementAtOrDefault(1),
                    "最后一个" => peaks[^1],
                    _ => 0,
                };
                if (torque == 0) return;
                // if (torque > 2 * Model.Tool!.SetTorque) return; // 丢弃扭矩测量操作失误引发的无效结果
                var test = new Test
                {
                    ToolId = Model.Tool.Id,
                    SetTorque = Model.Tool.SetTorque,
                    RealTorque = torque,
                    Diviation = (torque - Model.Tool.SetTorque) / Model.Tool.SetTorque,
                    AllowedDiviation = Model.AllowedDiviation,
                    TestTime = DateTime.Now
                };
                Model.LastTest = test;
                Model.Tests.Add(test);
                //if (!test.IsOK)
                //{
                //    if (MessageBox.Show(this, "数据NG，是否重新测量", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                //    {
                //        Model.ClearTests();
                //    }
                //}
                if (Model.Tests.Count >= 12 && Model.TestsAreOK)
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

        private void AllowedDiviationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var content = ((ComboBoxItem)e.AddedItems[0]!).Content;
            if (content is string s && double.TryParse(s.TrimEnd('%'), out var allowedDiviation))
            {
                Model.AllowedDiviation = allowedDiviation / 100;
            }
        }

        private void PeakIndexComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var content = ((ComboBoxItem)e.AddedItems[0]!).Content;
            if (content is string s)
            {
                Model.PeakIndex = s;
            }
        }
    }
}
