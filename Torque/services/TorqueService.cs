using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Torque
{
    class TorqueService : ITorqueService
    {
        public TorqueServiceOptions Options { get; set; }
        // 初始容量存储1分钟1000hz数据。
        public List<double> Results { get; } = new(60 * 1000);
        double a;
        double b;
        readonly byte[] buffer = new byte[4];
        Socket? socket;
        CancellationTokenSource? cts;

        public TorqueService(TorqueServiceOptions options)
        {
            Options = options;
            a = options.a ?? 15 * 1000 / options.Sensitivity / 248 / 65536;
            b = options.b;
        }

        public Task Zero()
        {
            return Task.CompletedTask;
        }

        public async Task<double> ReadAsync()
        {
            if (socket?.Connected == true)
            {
                throw new ApplicationException("TorqueService正在读取中，不要重复连接");
            }
            Results.Clear();
            var ip = Dns.GetHostAddresses(Options.Host)[0];
            socket = new(SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(ip, Options.Port);
            cts = new();
            return await Task.Run(() =>
            {
                while (true)
                {
                    if (socket.Receive(buffer) == 4 && buffer[2] == 0x0d && buffer[3] == 0x0a)
                    {
                        var value = BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(0, 2));
                        var torque = a * value + b;
                        Results.Add(torque);
                    }
                    else
                    {
                        throw new ApplicationException("TorqueService读取的数据帧错误");
                    }
                    if (cts.IsCancellationRequested)
                    {
                        if (socket.Available >= 4)
                        {
                            var bytes = new byte[socket.Available];
                            socket.Receive(bytes);
                            for (int i = 0; i <= bytes.Length - 4; i += 4)
                            {
                                var frame = bytes.AsSpan(i, 4);
                                if (frame[2] == 0x0d && frame[3] == 0x0a)
                                {
                                    var value = BinaryPrimitives.ReadInt16BigEndian(frame[0..2]);
                                    var torque = a * value + b;
                                    Results.Add(torque);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                        socket.Shutdown(SocketShutdown.Both);
                        socket.Dispose();
                        File.WriteAllText($"torque-{DateTime.Now:yyyyMMddHHmmss}.txt", string.Join("\r\n", Results));
                        Results.Sort((x, y) => y.CompareTo(x));
                        return Results.Take(Options.Sample).Average();
                    }
                }
            });
        }

        public void StopRead()
        {
            cts?.Cancel();
            cts?.Dispose();
        }
    }
}
