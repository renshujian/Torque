using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Torque
{
    public class StaticTorqueService : IDisposable
    {
        #region 参数
        public StaticTorqueServiceOptions Options { get; }
        private double a;
        private double b;
        private long interval = 300;
        private double _beginThreshold;
        private double _endThreshold;
        private TimeSpan _endTimeSpan;
        #endregion

        #region 信号
        public bool Connected => _socket.Connected;
        public double CurrentTorque => a * SensorValue + b;
        private readonly double[] _sensorValues;
        private int _sensorValueIndex;
        private double SensorValue => _sensorValues[_sensorValueIndex];
        #endregion

        #region 通讯
        private Socket _socket = new(SocketType.Stream, ProtocolType.Tcp);
        private CancellationTokenSource _cts = new();
        private Task _task = Task.CompletedTask;
        #endregion

        #region 业务，待剥离
        // 初始容量存储1分钟5000hz数据
        private readonly List<double> _results = new(60 * 5000);
        private Stopwatch _stopWatch = new();
        private long lastEndMilliseconds;
        private long beginMilliseconds;
        private bool recording;
        #endregion

        #region 事件，待整理
        public event Action<double[]>? StopRecording;
        public event Action<Exception>? OnError;
        public event Func<SocketException, bool>? OnSocketException;
        public event Action<double>? OnData;
        #endregion

        public StaticTorqueService(StaticTorqueServiceOptions options)
        {
            Options = options;
            if (options.SensorDataCount < 1 || options.SensorDataCount > 500)
            {
                throw new ArgumentException("SensorDataCount有效范围为[1,500]");
            }
            _sensorValues = new double[options.SensorDataCount];
        }

        public void Connect()
        {
            if (!Connected)
            {
                _socket = new(SocketType.Stream, ProtocolType.Tcp);
                _socket.ReceiveTimeout = 3000;
                _socket.Connect(Options.Host, Options.Port);
            }
            if (_task.IsCompleted)
            {
                _cts = new();
                _task = Task.Factory.StartNew(Read, TaskCreationOptions.LongRunning).ContinueWith(task =>
                {
                    if (task.Exception is not null)
                    {
                        OnError?.Invoke(task.Exception);
                    }
                });
                // 延时以保证有传感器读数
                Thread.Sleep(500);
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _socket.Close(3);
        }

        public void PrepareParameter(double targetValue)
        {
            Connect();
            var parameter = Options.GetParameter(targetValue);
            a = parameter.a ?? 15 * 1000 / parameter.Sensitivity / 248 / 65536;
            b = parameter.b ?? -a * _sensorValues.Average();
            interval = (long)parameter.Interval.TotalMilliseconds;
            _beginThreshold = parameter.BeginThreshold * targetValue;
            _endThreshold = parameter.EndThreshold * targetValue;
            _endTimeSpan = parameter.EndTimeSpan;
        }

        public void StartTest()
        {
            Reset();
            _stopWatch = Stopwatch.StartNew();
            OnData += Test;
        }

        public void StopTest()
        {
            OnData -= Test;
            _stopWatch.Stop();
        }

        private void Reset()
        {
            _results.Clear();
            lastEndMilliseconds = -interval;
            beginMilliseconds = 0;
            recording = false;
        }

        private void Test(double sensorValue)
        {
            var currentMilliseconds = _stopWatch.ElapsedMilliseconds;
            var torque = a * sensorValue + b;
            if (torque >= _beginThreshold)
            {
                if (!recording && currentMilliseconds - lastEndMilliseconds >= interval)
                {
                    recording = true;
                    beginMilliseconds = currentMilliseconds;
                }
                if (recording)
                {
                    _results.Add(torque);
                }
            }
            if (recording)
            {
                bool shouldEnd = _endTimeSpan > TimeSpan.Zero ?
                    currentMilliseconds - beginMilliseconds >= _endTimeSpan.TotalMilliseconds :
                    torque < _endThreshold;
                if (shouldEnd)
                {
                    recording = false;
                    lastEndMilliseconds = currentMilliseconds;
                    StopRecording?.Invoke(_results.ToArray());
                    _results.Clear();
                }
            }
        }

        private void Read()
        {
            var buffer = new byte[256];
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var length = _socket.Receive(buffer);
                    if (length < 1)
                    {
                        throw new SocketException();
                    }
                    // [byte1,byte2,0x0d,0x0a]为一个有效数据，先找到第一个有效数据
                    var beginIndex = length - 3;
                    for (int i = 0; i < length - 1; i++)
                    {
                        if (buffer[i] == 0x0d && buffer[i + 1] == 0x0a)
                        {
                            beginIndex = i >= 2 ? i - 2 : i + 2;
                            break;
                        }
                    }
                    // 按照4字节一组处理数据
                    for (int i = beginIndex; i < length - 3; i += 4)
                    {
                        // 必须先写数据再修改索引，才能保证另一个线程不会读到最旧的数据
                        int writeIndex = _sensorValueIndex == _sensorValues.Length - 1 ? 0 : _sensorValueIndex + 1;
                        _sensorValues[writeIndex] = BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(i, 2));
                        _sensorValueIndex = writeIndex;
                        OnData?.Invoke(SensorValue);
                    }
                }
                catch (SocketException e)
                {
                    // FIXME: 和窗口交互代码太乱，以及如果再抛错如何处理？
                    if (OnSocketException?.Invoke(e) == true)
                    {
                        _socket = new(SocketType.Stream, ProtocolType.Tcp);
                        _socket.ReceiveTimeout = 3000;
                        _socket.Connect(Options.Host, Options.Port);
                    }
                }
            }
        }
    }
}
