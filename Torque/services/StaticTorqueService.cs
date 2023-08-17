using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Torque
{
    public class StaticTorqueService
    {
        public StaticTorqueServiceOptions Options { get; set; }
        public double Threshold { get; set; }
        // 初始容量存储1分钟5000hz数据
        public List<double> Results { get; } = new(60 * 5000);
        double a;
        double b;
        long interval = 300;
        Socket? socket;
        CancellationTokenSource cts = new();
        Task task = Task.CompletedTask;

        public event Action<double[]>? StopRecording;
        public event Action<Exception>? OnError;
        public event Func<SocketException, bool>? OnSocketException;

        public StaticTorqueService(StaticTorqueServiceOptions options)
        {
            Options = options;
            a = options.a ?? 15 * 1000 / options.Sensitivity / 248 / 65536;
            b = options.b;
        }

        public Task Zero()
        {
            return Task.CompletedTask;
        }

        public void StartRead()
        {
            if (socket is not null)
            {
                throw new ApplicationException($"已有socket, connected: {socket.Connected}");
            }
            socket = new(SocketType.Stream, ProtocolType.Tcp);
            socket.ReceiveTimeout = 3000;
            socket.Connect(Options.Host, Options.Port);
            cts = new();
            task = Task.Run(Read).ContinueWith(task =>
            {
                socket.Close(3);
                socket = null;
                if (task.Exception is not null)
                {
                    OnError?.Invoke(task.Exception);
                }
            });
        }

        public Task StopRead()
        {
            if (task.IsCompleted) return task;
            cts.Cancel();
            cts.Dispose();
            return task;
        }

        void Read()
        {
            Results.Clear();
            var validPackets = 0;
            var buffer = new byte[256];
            var stopWatch = Stopwatch.StartNew();
            long lastMilliseconds = -interval;
            var recording = false;
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var length = socket!.Receive(buffer);
                    if (length < 1)
                    {
                        stopWatch.Stop();
                        throw new ApplicationException("socket连接异常");
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
                        validPackets++;
                        var milliseconds = stopWatch.ElapsedMilliseconds;
                        var value = BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(i, 2));
                        var torque = a * value + b;
                        if (torque >= Threshold)
                        {
                            if (!recording && milliseconds - lastMilliseconds >= interval)
                            {
                                recording = true;
                            }
                            if (recording)
                            {
                                Results.Add(torque);
                            }
                        }
                        else if (recording)
                        {
                            recording = false;
                            lastMilliseconds = milliseconds;
                            StopRecording?.Invoke(Results.ToArray());
                            Results.Clear();
                        }
                    }
                }
                catch (SocketException e)
                {
                    // FIXME: 和窗口交互代码太乱，以及如果再抛错如何处理？
                    if (OnSocketException?.Invoke(e) == true)
                    {
                        socket = new(SocketType.Stream, ProtocolType.Tcp);
                        socket.ReceiveTimeout = 3000;
                        socket.Connect(Options.Host, Options.Port);
                    }
                }
            }
            stopWatch.Stop();
            if (validPackets == 0)
            {
                throw new ApplicationException("没有收到有效数据");
            }
        }
    }
}
