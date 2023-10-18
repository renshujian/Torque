using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Torque
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const long CHART_CAPACITY = 1000;
        static readonly TimeSpanPoint NULL_POINT = new(TimeSpan.MinValue, double.MinValue);
        internal MainWindowModel Model { get; } = new();
        TorqueService TorqueService { get; }
        IMesService MesService { get; }
        AppDbContext AppDbContext { get; }
        IServiceProvider sp;
        Test? currentTest;
        BinaryWriter result = BinaryWriter.Null;
        List<TimeSpanPoint> chartValues;
        List<TimeSpanPoint> chartValues2 = new();
        TimeSpan lastChartAt;
        TimeSpan chartInterval = TimeSpan.FromSeconds(1);
        TimeSpanPoint maxPoint = NULL_POINT;
        TimeSpanPoint loosePoint = NULL_POINT;

        public MainWindow(TorqueServiceOptions torqueServiceOptions, IMesService mesService, AppDbContext appDbContext, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            DataContext = Model;
            chartValues = (List<TimeSpanPoint>)Model.Series[0].Values!;
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
            if (!ValidateSamplings()) return;
            var testTime = DateTime.Now;
            var resultPath = Path.Combine("results", $"{testTime:yyyyMMddHHmmss}");
            result = new BinaryWriter(File.Create(resultPath));
            var torqueServiceOptions = TorqueService.Options with
            {
                Sensitivity = Model.Sensitivity,
                a = Model.A,
                b = Model.B,
            };
            TorqueService.Options = torqueServiceOptions;
            currentTest = new Test()
            {
                ToolId = Model.Tool.Id,
                SetTorque = Model.Tool.SetTorque,
                AllowedDiviation = Model.AllowedDiviation,
                TestTime = testTime,
                ResultPath = resultPath,
                TorqueServiceOptions = torqueServiceOptions,
                Samplings = Model.Samplings.OrderBy(it => it.Time).ToArray(),
            };
            StopButton.Visibility = Visibility.Visible;
            Model.NotTesting = false;
            chartValues.Clear();
            maxPoint = NULL_POINT;
            loosePoint = NULL_POINT;
            chart.Sections = Array.Empty<RectangularSection>();
            chart.VisualElements = Array.Empty<LabelVisual>();
            chart.ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.None;
            ResetZoom(null!, null!);
            TorqueService.StartRead(currentTest.Samplings);
        }

        private void HandleData(TimeSpan time, double torque)
        {
            result.Write(torque);
            TimeSpanPoint point = new(time, torque);
            chartValues2.Add(point);
            if (chartValues2.Count > CHART_CAPACITY)
            {
                Dispatcher.Invoke(() =>
                {
                    var values = (List<TimeSpanPoint>)Model.Series[0].Values;
                    Model.Series[0].Values = chartValues2;
                    chartValues = chartValues2;
                    chartValues2 = values;
                    chartValues2.Clear();
                    chart.CoreChart.Update();
                    lastChartAt = time;
                });
            }
            else if (time - lastChartAt > chartInterval)
            {
                Dispatcher.Invoke(() =>
                {
                    chartValues.AddRange(chartValues2);
                    int removeCount = chartValues.Count - (int)CHART_CAPACITY;
                    if (removeCount > 0)
                    {
                        chartValues.RemoveRange(0, removeCount);
                    }
                    chartValues2.Clear();
                    chart.CoreChart.Update();
                    lastChartAt = time;
                });
            }
            if (torque > maxPoint.Value)
            {
                maxPoint = point;
                loosePoint = NULL_POINT;
            }
            if (loosePoint == NULL_POINT && torque < Model.LooseForce)
            {
                loosePoint = point;
            }
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
            currentTest.RealTorque = torque;
            currentTest.Diviation = (torque - currentTest.SetTorque) / currentTest.SetTorque;
            Model.LastTest = currentTest;
            Model.Tests.Add(currentTest);
            if (!currentTest.IsOK && MessageBox.Show(this, "数据NG，是否清除数据", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Model.ClearTests();
                return;
            }
            AppDbContext.Tests.Add(currentTest);
            AppDbContext.SaveChanges();

            if (Model.Tests.Count >= 12 && Model.TestsAreOK)
            {
                if (MessageBox.Show(this, "校准完成，是否上传数据", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    MesService.Upload(Model.Tests);
                    Model.ClearTests();
                }
            }
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
            if (maxPoint == NULL_POINT)
            {
                MessageBox.Show("没有测量数据");
                return;
            }
            AddTest(maxPoint.Value!.Value);
            if (loosePoint == NULL_POINT)
            {
                return;
            }
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

        private static List<TimeSpanPoint> getChartPoints(Test test, double minLimit, double maxLimit)
        {
            Debug.Assert(minLimit <= maxLimit);

            TimeSpan lastSampleEndTime = TimeSpan.Zero;
            double lastSampleEndOffset = 0;
            var samplings = test.Samplings.Select(it =>
            {
                double sampleEndOffset = lastSampleEndOffset + (it.Time - lastSampleEndTime).TotalSeconds * it.Frequency;
                var sample = (StartOffset: lastSampleEndOffset, EndOffset: sampleEndOffset, StartTime: lastSampleEndTime, it.Frequency);
                lastSampleEndTime = it.Time;
                lastSampleEndOffset = sampleEndOffset;
                return sample;
            }).ToArray();

            long packetsPerSecond = test.TorqueServiceOptions?.PacketsPerSecond ?? 5000;
            long ticksPerPacket = TimeSpan.TicksPerSecond / packetsPerSecond;
            double packetCount = new FileInfo(test.ResultPath).Length / 8.0;
            double minTick = Math.Max(minLimit, 0);
            double maxTick = Math.Min(maxLimit, test.Samplings.Last().Time.Ticks);
            double minOffset = getPacketOffset(minTick, test.Samplings);
            double maxOffset = getPacketOffset(maxTick, test.Samplings);
            double rangeCount = maxOffset - minOffset;
            double readInterval = rangeCount / CHART_CAPACITY;
            if (readInterval > 10)
            {
                using var handle = File.OpenHandle(test.ResultPath);
                var buffer = new byte[8];
                var chartPoints = new List<TimeSpanPoint>((int)CHART_CAPACITY);
                for (double i = minOffset; i <= maxOffset; i += readInterval)
                {
                    long packetOffset = (long)i;
                    var sampling = samplings.First(it => it.StartOffset <= packetOffset &&  it.EndOffset >= packetOffset);
                    TimeSpan time = sampling.StartTime + TimeSpan.FromSeconds((packetOffset - sampling.StartOffset) / sampling.Frequency);
                    RandomAccess.Read(handle, buffer, packetOffset * 8);
                    chartPoints.Add(new(time, BitConverter.ToDouble(buffer)));
                }
                return chartPoints;
            }
            else if (readInterval < 1.2)
            {
                long startOffset = (long)minOffset;
                long endOffset = (long)maxOffset;
                using var reader = new BinaryReader(File.OpenRead(test.ResultPath));
                reader.BaseStream.Position = startOffset * 8;
                var chartPoints = new List<TimeSpanPoint>((int)rangeCount + 1);
                for (long i = startOffset; i <= endOffset; i++)
                {
                    var sampling = samplings.First(it => it.StartOffset <= i && it.EndOffset >= i);
                    TimeSpan time = sampling.StartTime + TimeSpan.FromSeconds((i - sampling.StartOffset) / sampling.Frequency);
                    chartPoints.Add(new(time, reader.ReadDouble()));
                }
                return chartPoints;
            }
            else
            {
                long startOffset = (long)minOffset;
                using var stream = File.OpenRead(test.ResultPath);
                stream.Position = startOffset * 8;
                using var MemoryOwner = MemoryPool<byte>.Shared.Rent(((int)rangeCount + 1) * 8);
                int readBytes = 0;
                int loopCount = 0;
                while (readBytes < MemoryOwner.Memory.Length && loopCount < 100)
                {
                    readBytes += stream.Read(MemoryOwner.Memory.Slice(readBytes).Span);
                }
                var chartPoints = new List<TimeSpanPoint>((int)CHART_CAPACITY);
                for (double i = startOffset; i <= maxOffset; i += readInterval)
                {
                    long packetOffset = (long)i;
                    var sampling = samplings.First(it => it.StartOffset <= packetOffset && it.EndOffset >= packetOffset);
                    TimeSpan time = sampling.StartTime + TimeSpan.FromSeconds((packetOffset - sampling.StartOffset) / sampling.Frequency);
                    var buffer = MemoryOwner.Memory.Slice((int)(packetOffset - startOffset) * 8, 8);
                    chartPoints.Add(new(time, BitConverter.ToDouble(buffer.Span)));
                }
                return chartPoints;
            }
        }

        private static double getPacketOffset(double tick, Sampling[] samplings)
        {
            Debug.Assert(tick >= 0);
            Debug.Assert(samplings.Last().Time.Ticks >= tick);
            long lastTick = 0;
            double packetOffset = 0;
            foreach (var sampling in samplings)
            {
                if (sampling.Time.Ticks >= tick)
                {
                    packetOffset += (tick - lastTick) * sampling.Frequency / TimeSpan.TicksPerSecond;
                    return packetOffset;
                }
                else
                {
                    packetOffset += (sampling.Time.Ticks - lastTick) * sampling.Frequency / TimeSpan.TicksPerSecond;
                    lastTick = sampling.Time.Ticks;
                }
            }
            return packetOffset;
        }

        private void ZoomChartData(object sender, RoutedEventArgs e)
        {
            if (currentTest == null) return;
            var minLimit = Model.XAxes[0].MinLimit;
            if (minLimit == null) return;
            var maxLimit = Model.XAxes[0].MaxLimit;
            if (maxLimit == null) return;
            try
            {
                Model.Series[0].Values = getChartPoints(currentTest, minLimit.Value, maxLimit.Value);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private void ResetZoom(object sender, RoutedEventArgs e)
        {
            Model.Series[0].Values = chartValues;
            Model.XAxes[0].MinLimit = null;
            Model.XAxes[0].MaxLimit = null;
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

        private bool ValidateSamplings()
        {
            var samplings = Model.Samplings;
            if (samplings.Count == 0)
            {
                MessageBox.Show(this, "采样段不能为空");
                return false;
            }
            if (samplings.Any(it => it.Time <= TimeSpan.Zero))
            {
                MessageBox.Show(this, "采样时间节点应大于0");
                return false;
            }
            if (samplings.Any(it => it.Frequency <= 0))
            {
                MessageBox.Show(this, "采样频率应大于0");
                return false;
            }
            if (samplings.DistinctBy(it => it.Time).Count() < samplings.Count)
            {
                MessageBox.Show(this, "不能有重复的采样时间节点");
                return false;
            }
            return true;
        }
    }
}
