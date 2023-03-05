using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Torque
{
    public class TorqueService
    {
        public TorqueServiceOptions Options { get; set; }
        double a;
        double b;
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
            a = options.a ?? 10 * 1000 / options.Sensitivity / 248 / 65536;
            b = options.b;
        }

        public Task Zero()
        {
            return Task.CompletedTask;
        }

        public void StartRead(MainWindowModel.Sampling[] samplings)
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

        void Read(MainWindowModel.Sampling[] samplings)
        {
            var validPackets = 0;
            var buffer = new byte[4];
            var stopWatch = Stopwatch.StartNew();
            var sampling = samplings[0];
            var lastTime = -sampling.Interval;
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    if (socket!.Receive(buffer) == 4 && buffer[2] == 0x0d && buffer[3] == 0x0a)
                    {
                        validPackets++;
                        var time = stopWatch.Elapsed;
                        if (time > sampling.Time)
                        {
                            sampling = samplings.FirstOrDefault(it => it.Time > time, sampling);
                            if (time > sampling.Time)
                            {
                                // 没有下一个采样段了，结束循环
                                OnStop?.Invoke();
                                break;
                            }
                        }
                        if (time >= lastTime + sampling.Interval)
                        {
                            lastTime = time;
                            var value = BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(0, 2));
                            var torque = a * value + b;
                            OnData?.Invoke(time, torque);
                        }
                    }
                    else
                    {
                        // 尝试找到符合协议的数据包尾
                        while (!cts.IsCancellationRequested)
                        {
                            if (socket.Receive(buffer, 1, SocketFlags.None) < 1)
                            {
                                stopWatch.Stop();
                                throw new ApplicationException("socket连接异常");
                            }
                            if (buffer[0] == 0x0d)
                            {
                                socket.Receive(buffer, 1, SocketFlags.None);
                                if (buffer[0] == 0x0a)
                                {
                                    break; // 跳出当前逐字节读取数据的循环，结束else块进入下一轮主循环
                                }
                            }
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
