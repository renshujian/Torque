using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LiveChartsCore.Defaults;
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
        ObservableCollection<TimeSpanPoint> resultValues;

        public MainWindow(TorqueService torqueService, IMesService mesService, AppDbContext appDbContext, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            DataContext = Model;
            resultValues = (ObservableCollection<TimeSpanPoint>)Model.Series[0].Values!;
            TorqueService = torqueService;
            MesService = mesService;
            AppDbContext = appDbContext;
            sp = serviceProvider;
            TorqueService.OnData += HandleData;
            TorqueService.OnError += HandleError;
            TorqueService.OnSocketException += HandleSocketException;
            TorqueService.OnStop += HandleStop;
            Closed += (_, _) =>
            {
                TorqueService.OnData -= HandleData;
                TorqueService.OnError -= HandleError;
                TorqueService.OnSocketException -= HandleSocketException;
                TorqueService.OnStop -= HandleStop;
            };
            Directory.CreateDirectory("results");
        }

        private async void ResetTorque(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("要标定扭矩传感器零点并清除当前数据吗？", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await TorqueService.Zero();
                Model.ClearTests();
            }
        }

        private void ReadTorque(object sender, RoutedEventArgs e)
        {
            StopButton.Visibility = Visibility.Visible;
            Model.NotTesting = false;
            resultPath = Path.Combine("results", $"{DateTime.Now:yyyyMMddHHmmss}.csv");
            result = File.CreateText(resultPath);
            result.AutoFlush = true;
            result.WriteLine("milliseconds,torque");
            resultValues.Clear();
            chart.ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.None;
            Model.XAxes[0].MinLimit = null;
            Model.XAxes[0].MaxLimit = null;
            TorqueService.StartRead(Model.Samplings.ToArray());
        }

        private void HandleData(TimeSpan time, double torque)
        {
            resultValues.Add(new(time, torque));
            result?.WriteLine($"{time},{torque}");
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

        private void HandleStop() => Dispatcher.Invoke(() => StopButton_Click(null!, null!));

        private void AddTest(double torque)
        {
            var test = new Test
            {
                ToolId = Model.Tool.Id,
                SetTorque = Model.Tool.SetTorque,
                RealTorque = torque,
                Diviation = (torque - Model.Tool.SetTorque) / Model.Tool.SetTorque,
                AllowedDiviation = Model.AllowedDiviation,
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
            chart.ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.X;
            Model.NotTesting = true;
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

        private void AllowedDiviationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var content = ((ComboBoxItem)e.AddedItems[0]!).Content;
            if (content is string s && double.TryParse(s.TrimEnd('%'), out var allowedDiviation))
            {
                Model.AllowedDiviation = allowedDiviation / 100;
            }
        }

        private void AddSampling(object sender, RoutedEventArgs e)
        {
            var dialog = new AddSamplingDialog();
            if (dialog.ShowDialog() != true) return;

            if (!TimeSpan.TryParse(dialog.time.Text, out var time))
            {
                MessageBox.Show(this, "时间节点格式应为 天.小时:分钟:秒.小数秒");
                return;
            }
            if (time <= TimeSpan.Zero)
            {
                MessageBox.Show(this, "时间节点应大于0");
                return;
            }
            if (!TimeSpan.TryParse(dialog.interval.Text, out var interval))
            {
                MessageBox.Show(this, "采样间隔格式应为 天.小时:分钟:秒.小数秒");
                return;
            }
            if (interval <= TimeSpan.Zero)
            {
                MessageBox.Show(this, "采样间隔应大于0");
                return;
            }
            // 按照time单调递增插入
            for (int i = 0; i < Model.Samplings.Count; i++)
            {
                if (Model.Samplings[i].Time == time)
                {
                    MessageBox.Show(this, "不能添加重复的时间节点");
                    return;
                }
                else if (Model.Samplings[i].Time > time)
                {
                    Model.Samplings.Insert(i, new(time, interval));
                    return;
                }
            }
            // 已有项都小于现在要插入的time, 插入到列表尾
            Model.Samplings.Add(new(time, interval));
        }

        private void removeSampling(object sender, RoutedEventArgs e)
        {
            if (Model.Samplings.Count == 1)
            {
                MessageBox.Show(this, "采样段不能为空");
                return;
            }
            try
            {
                Model.Samplings.RemoveAt(samplingListBox.SelectedIndex);
            }
            catch { }
        }
    }
}
