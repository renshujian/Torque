using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using LiveChartsCore.SkiaSharpView.VisualElements;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

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

        public MainWindow(TorqueServiceOptions torqueServiceOptions, IMesService mesService, AppDbContext appDbContext, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            DataContext = Model;
            resultValues = (ObservableCollection<TimeSpanPoint>)Model.Series[0].Values!;
            Model.Sensitivity = torqueServiceOptions.Sensitivity;
            Model.A = torqueServiceOptions.a;
            Model.B = torqueServiceOptions.b;
            // TODO: 将TorqueServie从窗口属性中移除，在开始测量时构建（如何停止？）
            TorqueService = new TorqueService(torqueServiceOptions);
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
            if (MessageBox.Show("要标定传感器零点并清除当前数据吗？", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await TorqueService.Zero();
                Model.ClearTests();
            }
        }

        private void ReadTorque(object sender, RoutedEventArgs e)
        {
            ValidateSamplings();
            TorqueService.Options = TorqueService.Options with
            {
                Sensitivity = Model.Sensitivity,
                a = Model.A,
                b = Model.B,
            };
            StopButton.Visibility = Visibility.Visible;
            Model.NotTesting = false;
            resultPath = Path.Combine("results", $"{DateTime.Now:yyyyMMddHHmmss}.csv");
            result = File.CreateText(resultPath);
            result.WriteLine("milliseconds,torque");
            resultValues.Clear();
            chart.ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.None;
            Model.XAxes[0].MinLimit = null;
            Model.XAxes[0].MaxLimit = null;
            TorqueService.StartRead(Model.Samplings.OrderBy(it => it.Time).ToArray());
        }

        private void HandleData(TimeSpan time, double torque)
        {
            Dispatcher.InvokeAsync(() =>
            {
                resultValues.Add(new(time, torque));
                result?.WriteLine($"{time},{torque}");
            });
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
            if (resultValues.Count == 0)
            {
                MessageBox.Show("没有测量数据");
                return;
            }
            AddTest(resultValues.Max(point => point.Value.GetValueOrDefault()));

            int? looseIndex = null;
            for (int i = resultValues.Count - 1; i >= 0 ; i--)
            {
                if (resultValues[i].Value < Model.LooseForce)
                {
                    looseIndex = i;
                }
                else
                {
                    break;
                }
            }
            if (looseIndex == null)
            {
                return;
            }

            var loosePoint = resultValues[looseIndex.Value];
            chart.Sections = new RectangularSection[]
            {
                    new RectangularSection
                    {
                        Xi = loosePoint.Coordinate.SecondaryValue,
                        Xj = loosePoint.Coordinate.SecondaryValue,
                        Yj = 25,
                        Stroke = new SolidColorPaint(SKColors.Black)
                        {
                            PathEffect = new DashEffect(new float[] { 5, 5 })
                        },
                    }
            };
            chart.VisualElements = new LabelVisual[]
            {
                    new LabelVisual
                    {
                        Text = $"X: {loosePoint.TimeSpan}\r\nY: {loosePoint.Value}",
                        X = loosePoint.Coordinate.SecondaryValue,
                        Paint = new SolidColorPaint(SKColors.Red) { ZIndex = 10 },
                        LocationUnit = LiveChartsCore.Measure.MeasureUnit.ChartValues,
                    }
            };
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

        private void ValidateSamplings()
        {
            var samplings = Model.Samplings;
            if (samplings.Count == 0)
            {
                MessageBox.Show(this, "采样段不能为空");
                return;
            }
            if (samplings.Any(it => it.Time <= TimeSpan.Zero))
            {
                MessageBox.Show(this, "采样时间节点应大于0");
                return;
            }
            if (samplings.Any(it => it.Frequency <= 0))
            {
                MessageBox.Show(this, "采样频率应大于0");
                return;
            }
            if (samplings.DistinctBy(it => it.Time).Count() < samplings.Count)
            {
                MessageBox.Show(this, "不能有重复的采样时间节点");
                return;
            }
        }
    }
}
