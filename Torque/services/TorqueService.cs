using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
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
        int interval;
        Socket? socket;
        CancellationTokenSource cts = new();

        public event Action<long, double>? OnData;
        public event Action<Exception>? OnError;

        public TorqueService(TorqueServiceOptions options)
        {
            Options = options;
            a = options.a ?? 10 * 1000 / options.Sensitivity / 248 / 65536;
            b = options.b;
            interval = 1000 / options.Hz;
        }

        public Task Zero()
        {
            return Task.CompletedTask;
        }

        public void StartRead()
        {
            if (socket is not null)
            {
                return;
            }
            socket = new(SocketType.Stream, ProtocolType.Tcp);
            var ip = Dns.GetHostAddresses(Options.Host)[0];
            socket.Connect(ip, Options.Port);
            cts = new();
            Task.Run(Read).ContinueWith(task =>
            {
                // cancel或socket异常才进入这段代码
                // cancel时已经shutdown socket
                // socket异常时不用shutdown
                socket.Dispose();
                socket = null;
                if (task.Exception is not null)
                {
                    OnError?.Invoke(task.Exception);
                }
            });
        }

        public void StopRead()
        {
            if (cts.IsCancellationRequested) return;
            cts.Cancel();
            cts.Dispose();
            // Read循环在socket.Receive时可能一直阻塞，需要断开连接来响应cancel
            socket?.Shutdown(SocketShutdown.Both);
        }

        void Read()
        {
            var buffer = new byte[4];
            var stopWatch = Stopwatch.StartNew();
            long lastMilliseconds = -interval;
            while (!cts.IsCancellationRequested)
            {
                if (socket!.Receive(buffer) == 4 && buffer[2] == 0x0d && buffer[3] == 0x0a)
                {
                   var milliseconds = stopWatch.ElapsedMilliseconds;
                    if (milliseconds >= lastMilliseconds + interval)
                    {
                        lastMilliseconds = milliseconds;
                        var value = BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(0, 2));
                        var torque = a * value + b;
                        OnData?.Invoke(milliseconds, torque);
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
            stopWatch.Stop();
        }
    }
}
