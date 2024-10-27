using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

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
        DispatcherTimer _timer = new();

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
            Directory.CreateDirectory("reports");
        }

        private void ResetTorque(object sender, RoutedEventArgs e)
        {
            if (Model.Tool is not null)
            {
                TorqueService.PrepareParameter(Model.Tool.SetTorque);
                Model.ClearTests();
                Model.CanTest = true;
            }
        }

        private void ReadTorque(object sender, RoutedEventArgs e)
        {
            StopButton.Visibility = Visibility.Visible;
            ZeroButton.IsEnabled = false;
            TorqueService.StartTest();
        }

        private void HandleError(Exception e)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(e.Message, e.GetType().FullName, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private bool HandleSocketException(SocketException e)
        {
            return Dispatcher.Invoke(() =>
            {
                if (MessageBox.Show($"({e.ErrorCode}){e.Message}", "传感器连接异常，是否重连？", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
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

        private void AddTest(double[] rawData)
        {
            string timestamp = $"{DateTime.Now:yyyyMMddHHmmss}";
            File.WriteAllTextAsync(Path.Combine("results", $"{timestamp}.txt"), string.Join("\r\n", rawData));

            var parameter = TorqueService.Options.GetParameter(Model.Tool.SetTorque);
            if (rawData.Length <= parameter.BeginSkip + parameter.EndSkip) return;

            Dispatcher.BeginInvoke(() =>
            {
                double[] data = rawData[parameter.BeginSkip..^parameter.EndSkip];
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
                if (!test.IsOK)
                {
                    if (MessageBox.Show(this, "数据NG，是否重新测量", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        Model.ClearTests();
                    }
                }
                if (Model.Tests.Count >= TorqueService.Options.SaveCount)
                {
                    SaveCsv(Model.Tests, Path.Combine("reports", $"{Model.Tool?.Id}_{DateTime.Now:yyyyMMddHHmmss}.csv"));
                }
            });
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            TorqueService.StopTest();
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
                    Model.CanTest = false;
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
                    Model.CanTest = false;
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

        private void PeakIndexComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var content = ((ComboBoxItem)e.AddedItems[0]!).Content;
            if (content is string s)
            {
                Model.PeakIndex = s;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (TorqueService.Connected)
            {
                CurrentTorque.Content = TorqueService.CurrentTorque;
            }
            else
            {
                CurrentTorque.Content = null;
            }
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
        }

        private void SaveCsv(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog()
            {
                FileName = $"{Model.Tool?.Id}_{DateTime.Now:yyyyMMddHHmmss}",
                DefaultExt = "csv",
                Filter = "CSV|*.csv",
            };
            if (dialog.ShowDialog() == true)
            {
                SaveCsv(Model.Tests, dialog.FileName);
            }
        }

        private void SaveCsv(IEnumerable<Test> tests, string fileName)
        {
            using var writer = new StreamWriter(fileName, false, Encoding.Unicode);
            writer.WriteLine("日期\t时间\t实测扭矩值\t目标扭矩值\t是否合格");
            foreach (var test in tests)
            {
                writer.WriteLine($"{test.TestTime.ToShortDateString()}\t{test.TestTime.ToLongTimeString()}\t{test.RealTorque}\t{test.SetTorque}\t{(test.IsOK ? "PASS" : "NG")}");
            }
        }
    }
}
