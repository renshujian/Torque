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

        public TorqueService(TorqueServiceOptions options)
        {
            Options = options;
            a = options.a ?? 15 * 1000 / options.Sensitivity / 248 / 65536;
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
            Task.Run(Read); // TODO: 后台线程错误未传递
        }

        public void StopRead()
        {
            if (cts.IsCancellationRequested) return;
            cts.Cancel();
            cts.Dispose();
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
                    throw new ApplicationException("TorqueService读取的数据帧错误");
                }
            }
            stopWatch.Stop();
            socket!.Shutdown(SocketShutdown.Both);
            socket.Dispose();
            socket = null;
        }
    }
}
