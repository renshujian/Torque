using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using LiveChartsCore.SkiaSharpView.VisualElements;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
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
            chartValues2.Clear();
            lastChartAt = TimeSpan.Zero;
            maxPoint = NULL_POINT;
            loosePoint = NULL_POINT;
            chart.Sections = Array.Empty<RectangularSection>();
            chart.VisualElements = Array.Empty<LabelVisual>();
            chart.ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.None;
            Model.Series[0].Values = chartValues;
            Model.XAxes[0].MinLimit = null;
            Model.XAxes[0].MaxLimit = null;
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
            // TODO: 合并LastTest和currentTest
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
            Debug.Assert(minLimit < maxLimit);

            // 将test.Samplings处理成方便time和offset相互转化的数据结构
            TimeSpan lastSampleEndTime = TimeSpan.Zero;
            double lastSampleEndOffset = 0;
            var samplings = test.Samplings.Select(it =>
            {
                double sampleEndOffset = lastSampleEndOffset + (it.Time - lastSampleEndTime).TotalSeconds * it.Frequency;
                var sample = (StartOffset: lastSampleEndOffset, EndOffset: sampleEndOffset, StartTime: lastSampleEndTime, EndTime: it.Time, it.Frequency);
                lastSampleEndTime = sample.EndTime;
                lastSampleEndOffset = sample.EndOffset;
                return sample;
            }).ToArray();
            double timeToOffset(TimeSpan time)
            {
                var sampling = samplings.First(it => it.StartTime <= time && it.EndTime >= time);
                return sampling.StartOffset + (time - sampling.StartTime).TotalSeconds * sampling.Frequency;
            }
            TimeSpan offsetToTime(long offset)
            {
                var sampling = samplings.First(it => it.StartOffset <= offset && it.EndOffset >= offset);
                return sampling.StartTime + TimeSpan.FromSeconds((offset - sampling.StartOffset) / sampling.Frequency);
            }

            double packetCount = new FileInfo(test.ResultPath).Length / 8.0;
            double minTick = Math.Max(minLimit, 0);
            double maxTick = Math.Min(maxLimit, test.Samplings.Last().Time.Ticks);
            double minOffset = timeToOffset(TimeSpan.FromTicks((long)minTick));
            double maxOffset = Math.Min(timeToOffset(TimeSpan.FromTicks((long)maxTick)), packetCount - 1);
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
                    RandomAccess.Read(handle, buffer, packetOffset * 8);
                    chartPoints.Add(new(offsetToTime(packetOffset), BitConverter.ToDouble(buffer)));
                }
                return chartPoints;
            }
            // readInterval不大于10说明rangeCount在int32的范围内
            else if (readInterval < 1.2)
            {
                long startOffset = (long)minOffset;
                long endOffset = (long)maxOffset;
                using var reader = new BinaryReader(File.OpenRead(test.ResultPath));
                reader.BaseStream.Position = startOffset * 8;
                var chartPoints = new List<TimeSpanPoint>((int)rangeCount + 1);
                for (long i = startOffset; i <= endOffset; i++)
                {
                    chartPoints.Add(new(offsetToTime(i), reader.ReadDouble()));
                }
                return chartPoints;
            }
            else
            {
                long startOffset = (long)minOffset;
                using var stream = File.OpenRead(test.ResultPath);
                stream.Position = startOffset * 8;
                int byteCount = ((int)rangeCount + 1) * 8;
                using var MemoryOwner = MemoryPool<byte>.Shared.Rent(byteCount);
                int readBytes = 0;
                while (readBytes < byteCount)
                {
                    readBytes += stream.Read(MemoryOwner.Memory.Slice(readBytes).Span);
                }
                var chartPoints = new List<TimeSpanPoint>((int)CHART_CAPACITY);
                // TODO: 不清楚会不会出现超出byteCount的情况
                for (double i = startOffset; i <= maxOffset; i += readInterval)
                {
                    long packetOffset = (long)i;
                    var buffer = MemoryOwner.Memory.Slice((int)(packetOffset - startOffset) * 8, 8);
                    chartPoints.Add(new(offsetToTime(packetOffset), BitConverter.ToDouble(buffer.Span)));
                }
                return chartPoints;
            }
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
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private void ResetZoom(object sender, RoutedEventArgs e)
        {
            if (currentTest == null) return;
            Model.XAxes[0].MinLimit = 0;
            Model.XAxes[0].MaxLimit = currentTest.Samplings.Last().Time.Ticks;
            ZoomChartData(null!, null!);
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
            if (samplings.Any(it => it.Frequency > TorqueService.Options.PacketsPerSecond))
            {
                MessageBox.Show(this, $"采样频率应小于等于{TorqueService.Options.PacketsPerSecond}");
                return false;
            }
            if (samplings.DistinctBy(it => it.Time).Count() < samplings.Count)
            {
                MessageBox.Show(this, "不能有重复的采样时间节点");
                return false;
            }
            return true;
        }

        private void SaveCsv(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.DataContext is Test test)
            {
                var dialog = new SaveFileDialog()
                {
                    FileName = Path.GetFileNameWithoutExtension(test.ResultPath),
                    DefaultExt = "csv",
                    Filter = "CSV|*.csv",
                };
                if (dialog.ShowDialog() == true)
                {
                    var writer = File.CreateText(dialog.FileName);
                    writer.WriteLine("time,value");
                    var reader = new BinaryReader(File.OpenRead(test.ResultPath));

                    var progress = new ProgressDialog();
                    progress.bar.Maximum = reader.BaseStream.Length;
                    var timer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(500)
                    };
                    timer.Tick += (_, _) => progress.bar.Value = reader.BaseStream.Position;
                    timer.Start();

                    using var cts = new CancellationTokenSource();
                    Task.Run(() =>
                    {
                        var time = TimeSpan.Zero;
                        var sampling = test.Samplings[0];
                        var interval = TimeSpan.FromSeconds(1 / sampling.Frequency);
                        try
                        {
                            while (!cts.IsCancellationRequested)
                            {
                                var value = reader.ReadDouble();
                                writer.WriteLine($"{time},{value}");
                                if (time > sampling.Time)
                                {
                                    sampling = test.Samplings.First(it => it.Time > time);
                                    interval = TimeSpan.FromSeconds(1 / sampling.Frequency);
                                }
                                time += interval;
                            }
                        }
                        catch (EndOfStreamException)
                        {
                            Dispatcher.InvokeAsync(() => progress.DialogResult = true);
                        }
                        catch (InvalidOperationException)
                        {
                            Dispatcher.InvokeAsync(() => progress.DialogResult = true);
                        }
                        finally
                        {
                            timer.Stop();
                            reader.Dispose();
                            writer.Dispose();
                        }
                    }, cts.Token);

                    if (progress.ShowDialog() != true)
                    {
                        cts.Cancel();
                    }
                }
            }
        }
    }
}
