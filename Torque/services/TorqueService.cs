using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Torque
{
    public class TorqueService
    {
        TorqueServiceOptions options;
        public TorqueServiceOptions Options {
            get => options;
            [MemberNotNull("options")]
            set
            {
                options = value;
                A = value.a ?? 10 * 1000 / value.Sensitivity / 248 / 65536;
                B = value.b;
            }
        }
        public double A { get; private set; }
        public double B { get; private set; }
        Socket? socket;
        CancellationTokenSource cts = new();
        Task task = Task.CompletedTask;

        public event Action<TimeSpan, double>? OnData;
        public event Action<Exception>? OnError;
        public event Func<SocketException, bool>? OnSocketException;
        public event Action? OnStop;

        public TorqueService(TorqueServiceOptions options)
        {
            Options = options;
        }

        public short GetValue()
        {
            using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.ReceiveTimeout = 3000;
            socket.Connect(Options.Host, Options.Port);

            // TODO: 这里复制了Read的部分代码来从流中提取有效数据。应该重构出Decode函数
            var buffer = new byte[16];
            int length = socket.Receive(buffer);
            if (length < 4)
            {
                throw new ApplicationException("socket连接异常");
            }

            // [byte1,byte2,0x0d,0x0a]为一个有效数据，先找到第一个有效数据
            int? beginIndex = null;
            for (int i = 0; i < length - 1; i++)
            {
                if (buffer[i] == 0x0d && buffer[i + 1] == 0x0a)
                {
                    beginIndex = i >= 2 ? i - 2 : i + 2;
                    break;
                }
            }
            if (beginIndex == null)
            {
                throw new ApplicationException("没有收到有效数据");
            }

            return BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(beginIndex.Value, 2));
        }

        public void StartRead(Sampling[] samplings)
        {
            if (socket is not null)
            {
                throw new ApplicationException($"已有socket, connected: {socket.Connected}");
            }
            socket = new(SocketType.Stream, ProtocolType.Tcp);
            socket.ReceiveTimeout = 3000;
            socket.Connect(Options.Host, Options.Port);
            cts = new();
            task = Task.Run(() => Read(samplings)).ContinueWith(task =>
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

        void Read(Sampling[] samplings)
        {
            long packetIndex = -1;
            long ticksPerPacket = TimeSpan.TicksPerSecond / Options.PacketsPerSecond;
            var buffer = new byte[4096];
            var sampling = samplings[0];
            double sampleInterval = Options.PacketsPerSecond / sampling.Frequency;
            double sampleAt = 0;
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    int length = socket!.Receive(buffer);
                    if (length < 1)
                    {
                        throw new ApplicationException("socket连接异常");
                    }
                    // [byte1,byte2,0x0d,0x0a]为一个有效数据，先找到第一个有效数据
                    int beginIndex = length - 3;
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
                        packetIndex++;
                        var time = TimeSpan.FromTicks(packetIndex * ticksPerPacket);
                        if (time > sampling.Time)
                        {
                            sampling = samplings.FirstOrDefault(it => it.Time > time, sampling);
                            if (time > sampling.Time)
                            {
                                // 没有下一个采样段了，结束循环
                                OnStop?.Invoke();
                                return;
                            }
                            else
                            {
                                sampleInterval = Options.PacketsPerSecond / sampling.Frequency;
                            }
                        }
                        if (packetIndex >= sampleAt)
                        {
                            sampleAt += sampleInterval;
                            var value = BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(i, 2));
                            var torque = A * value + B;
                            OnData?.Invoke(time, torque);
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
        }
    }
}
